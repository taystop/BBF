using BBF.Api.Auth;
using BBF.Data.Entities;
using BBF.Shared.Contracts.Budget;
using Microsoft.EntityFrameworkCore;

namespace BBF.Api.Services;

public class BudgetPeriodsService(ITenantDbContextFactory tenantDb)
{
    public async Task<BudgetPeriodDto?> GetPeriodAsync(int groupId, DateTime monthStart, CancellationToken ct = default)
    {
        await using var db = await tenantDb.CreateForGroupAsync(groupId, ct);
        var period = await db.BudgetPeriods
            .Include(p => p.LineItems).ThenInclude(li => li.Category)
            .FirstOrDefaultAsync(p => p.StartDate == monthStart, ct);
        return period is null ? null : ToDto(period);
    }

    public async Task<BudgetPeriodDto> GetOrCreateCurrentPeriodAsync(int groupId, CancellationToken ct = default)
    {
        var monthStart = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);

        await using var db = await tenantDb.CreateForGroupAsync(groupId, ct);
        var period = await db.BudgetPeriods
            .Include(p => p.LineItems).ThenInclude(li => li.Category)
            .FirstOrDefaultAsync(p => p.StartDate == monthStart, ct);

        if (period is null)
        {
            period = new BudgetPeriod
            {
                GroupId = groupId,
                StartDate = monthStart,
                EndDate = monthStart.AddMonths(1),
                Status = "Open"
            };
            db.BudgetPeriods.Add(period);
            await db.SaveChangesAsync(ct);
        }

        var categories = await db.BudgetCategories
            .Where(c => c.IsActive)
            .ToListAsync(ct);

        var existingCategoryIds = period.LineItems.Select(li => li.CategoryId).ToHashSet();
        var added = false;
        foreach (var category in categories.Where(c => !existingCategoryIds.Contains(c.Id)))
        {
            db.BudgetLineItems.Add(new BudgetLineItem
            {
                BudgetPeriodId = period.Id,
                CategoryId = category.Id,
                BudgetedAmount = category.MonthlyLimit,
                RolledOverAmount = 0
            });
            added = true;
        }

        if (added)
        {
            await db.SaveChangesAsync(ct);
            period = await db.BudgetPeriods
                .Include(p => p.LineItems).ThenInclude(li => li.Category)
                .FirstAsync(p => p.Id == period.Id, ct);
        }

        return ToDto(period);
    }

    public async Task UpsertLineItemAsync(int groupId, int periodId, UpsertLineItemRequest request, CancellationToken ct = default)
    {
        await using var db = await tenantDb.CreateForGroupAsync(groupId, ct);

        // BudgetLineItem carries no GroupId of its own — confirm the period belongs to this tenant
        // (the query filter on BudgetPeriods already enforces this) before touching its line items.
        var periodExists = await db.BudgetPeriods.AnyAsync(p => p.Id == periodId, ct);
        if (!periodExists) return;

        var lineItem = await db.BudgetLineItems
            .FirstOrDefaultAsync(li => li.BudgetPeriodId == periodId && li.CategoryId == request.CategoryId, ct);

        if (lineItem is null)
        {
            db.BudgetLineItems.Add(new BudgetLineItem
            {
                BudgetPeriodId = periodId,
                CategoryId = request.CategoryId,
                BudgetedAmount = request.BudgetedAmount,
                RolledOverAmount = 0
            });
        }
        else
        {
            lineItem.BudgetedAmount = request.BudgetedAmount;
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task ClosePeriodAsync(int groupId, int periodId, CancellationToken ct = default)
    {
        await using var db = await tenantDb.CreateForGroupAsync(groupId, ct);
        var period = await db.BudgetPeriods
            .Include(p => p.LineItems).ThenInclude(li => li.Category)
            .FirstOrDefaultAsync(p => p.Id == periodId, ct);
        if (period is null || period.Status == "Closed") return;

        var nextLineItems = new List<(int CategoryId, decimal BudgetedAmount, decimal RolledOverAmount)>();

        foreach (var lineItem in period.LineItems)
        {
            var spent = await db.Transactions
                .Where(t => t.CategoryId == lineItem.CategoryId
                    && t.Date >= period.StartDate && t.Date < period.EndDate
                    && t.Amount > 0)
                .SumAsync(t => t.Amount, ct);

            var remaining = lineItem.BudgetedAmount + lineItem.RolledOverAmount - spent;
            var rolloverForNext = lineItem.Category.RolloverBehavior == "RollOver" ? remaining : 0m;

            nextLineItems.Add((lineItem.CategoryId, lineItem.BudgetedAmount, rolloverForNext));
        }

        period.Status = "Closed";
        period.ClosedAt = DateTime.UtcNow;

        var nextPeriod = new BudgetPeriod
        {
            GroupId = groupId,
            StartDate = period.EndDate,
            EndDate = period.EndDate.AddMonths(1),
            Status = "Open"
        };
        db.BudgetPeriods.Add(nextPeriod);
        await db.SaveChangesAsync(ct);

        foreach (var (categoryId, budgetedAmount, rolledOverAmount) in nextLineItems)
        {
            db.BudgetLineItems.Add(new BudgetLineItem
            {
                BudgetPeriodId = nextPeriod.Id,
                CategoryId = categoryId,
                BudgetedAmount = budgetedAmount,
                RolledOverAmount = rolledOverAmount
            });
        }

        await db.SaveChangesAsync(ct);
    }

    private static BudgetPeriodDto ToDto(BudgetPeriod p) => new(
        p.Id, p.StartDate, p.EndDate, p.Status,
        p.LineItems.Select(li => new BudgetLineItemDto(
            li.Id, li.CategoryId, li.Category.Name, li.Category.Color, li.BudgetedAmount, li.RolledOverAmount)).ToList());
}
