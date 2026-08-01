using BBF.Data;
using BBF.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace BBF.Services;

public class BudgetPeriodService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;

    public BudgetPeriodService(IDbContextFactory<ApplicationDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    /// <summary>
    /// Returns the period for the given month, or null if none exists yet — past months are never
    /// fabricated (see no-backfill decision). Only the current month's period is ever auto-created.
    /// </summary>
    public async Task<BudgetPeriod?> GetPeriodAsync(int groupId, DateTime monthStart)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.BudgetPeriods
            .Include(p => p.LineItems).ThenInclude(li => li.Category)
            .FirstOrDefaultAsync(p => p.GroupId == groupId && p.StartDate == monthStart);
    }

    /// <summary>
    /// Gets or creates the period for the current calendar month, seeding line items from
    /// BudgetCategory.MonthlyLimit for any active category that doesn't have one yet.
    /// </summary>
    public async Task<BudgetPeriod> GetOrCreateCurrentPeriodAsync(int groupId)
    {
        var monthStart = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);

        await using var db = await _dbFactory.CreateDbContextAsync();
        var period = await db.BudgetPeriods
            .Include(p => p.LineItems).ThenInclude(li => li.Category)
            .FirstOrDefaultAsync(p => p.GroupId == groupId && p.StartDate == monthStart);

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
            await db.SaveChangesAsync();
        }

        var categories = await db.BudgetCategories
            .Where(c => c.IsActive && c.GroupId == groupId)
            .ToListAsync();

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
            await db.SaveChangesAsync();
            return await db.BudgetPeriods
                .Include(p => p.LineItems).ThenInclude(li => li.Category)
                .FirstAsync(p => p.Id == period.Id);
        }

        return period;
    }

    public async Task UpsertLineItemAsync(int periodId, int categoryId, decimal budgetedAmount)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var lineItem = await db.BudgetLineItems
            .FirstOrDefaultAsync(li => li.BudgetPeriodId == periodId && li.CategoryId == categoryId);

        if (lineItem is null)
        {
            db.BudgetLineItems.Add(new BudgetLineItem
            {
                BudgetPeriodId = periodId,
                CategoryId = categoryId,
                BudgetedAmount = budgetedAmount,
                RolledOverAmount = 0
            });
        }
        else
        {
            lineItem.BudgetedAmount = budgetedAmount;
        }

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Closes a period: computes each category's RolledOverAmount based on RolloverBehavior, marks the
    /// period Closed, then opens the next period seeded from this period's line items (not MonthlyLimit).
    /// </summary>
    public async Task ClosePeriodAsync(int periodId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var period = await db.BudgetPeriods
            .Include(p => p.LineItems).ThenInclude(li => li.Category)
            .FirstOrDefaultAsync(p => p.Id == periodId);
        if (period is null || period.Status == "Closed") return;

        var nextLineItems = new List<(int CategoryId, decimal BudgetedAmount, decimal RolledOverAmount)>();

        foreach (var lineItem in period.LineItems)
        {
            var spent = await db.Transactions
                .Where(t => t.CategoryId == lineItem.CategoryId
                    && t.GroupId == period.GroupId
                    && t.Date >= period.StartDate && t.Date < period.EndDate
                    && t.Amount > 0)
                .SumAsync(t => t.Amount);

            var remaining = lineItem.BudgetedAmount + lineItem.RolledOverAmount - spent;
            var rolloverForNext = lineItem.Category.RolloverBehavior == "RollOver" ? remaining : 0m;

            nextLineItems.Add((lineItem.CategoryId, lineItem.BudgetedAmount, rolloverForNext));
        }

        period.Status = "Closed";
        period.ClosedAt = DateTime.UtcNow;

        var nextPeriod = new BudgetPeriod
        {
            GroupId = period.GroupId,
            StartDate = period.EndDate,
            EndDate = period.EndDate.AddMonths(1),
            Status = "Open"
        };
        db.BudgetPeriods.Add(nextPeriod);
        await db.SaveChangesAsync();

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

        await db.SaveChangesAsync();
    }
}
