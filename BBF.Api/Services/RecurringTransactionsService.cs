using BBF.Api.Auth;
using BBF.Data.Entities;
using BBF.Shared.Contracts.Recurring;
using Microsoft.EntityFrameworkCore;

namespace BBF.Api.Services;

public class RecurringTransactionsService(ITenantDbContextFactory tenantDb)
{
    public async Task<List<RecurringTransactionDto>> GetActiveAsync(int groupId, CancellationToken ct = default)
    {
        await using var db = await tenantDb.CreateForGroupAsync(groupId, ct);
        var today = DateTime.Today;
        return await db.RecurringTransactions
            .Include(r => r.Category)
            .Include(r => r.Account)
            .OrderBy(r => r.NextOccurrence)
            .Select(r => new RecurringTransactionDto(
                r.Id, r.Payee, r.Amount, r.CategoryId, r.Category != null ? r.Category.Name : null,
                r.AccountId, r.Account.Name, r.Frequency, r.NextOccurrence, r.EndDate, r.IsActive,
                r.AutoPost, r.MatchText,
                // Next bill still waiting to be paid; AutoPost rules have no bills, so fall back to NextOccurrence.
                db.BillOccurrences
                    .Where(b => b.RecurringTransactionId == r.Id && b.Status == BillStatuses.Expected && b.DueDate >= today.AddDays(-35))
                    .OrderBy(b => b.DueDate)
                    .Select(b => (DateTime?)b.DueDate)
                    .FirstOrDefault() ?? r.NextOccurrence))
            .ToListAsync(ct);
    }

    public async Task<RecurringTransactionDto> CreateAsync(int groupId, CreateRecurringTransactionRequest request, CancellationToken ct = default)
    {
        await using var db = await tenantDb.CreateForGroupAsync(groupId, ct);
        var rule = new RecurringTransaction
        {
            GroupId = groupId,
            Payee = request.Payee,
            Amount = request.Amount,
            CategoryId = request.CategoryId,
            AccountId = request.AccountId,
            Frequency = request.Frequency,
            NextOccurrence = request.NextOccurrence.Date,
            EndDate = request.EndDate,
            IsActive = true,
            AutoPost = request.AutoPost,
            MatchText = Clean(request.MatchText),
        };
        db.RecurringTransactions.Add(rule);
        await db.SaveChangesAsync(ct);

        await RefreshBillsAsync(db, rule, ct);

        await db.Entry(rule).Reference(r => r.Category).LoadAsync(ct);
        await db.Entry(rule).Reference(r => r.Account).LoadAsync(ct);
        return ToDto(rule);
    }

    public async Task UpdateAsync(int groupId, int ruleId, UpdateRecurringTransactionRequest request, CancellationToken ct = default)
    {
        await using var db = await tenantDb.CreateForGroupAsync(groupId, ct);
        var entity = await db.RecurringTransactions.FirstOrDefaultAsync(r => r.Id == ruleId, ct);
        if (entity is null) return;

        entity.Payee = request.Payee;
        entity.Amount = request.Amount;
        entity.CategoryId = request.CategoryId;
        entity.AccountId = request.AccountId;
        entity.Frequency = request.Frequency;
        entity.NextOccurrence = request.NextOccurrence.Date;
        entity.EndDate = request.EndDate;
        entity.IsActive = request.IsActive;
        entity.AutoPost = request.AutoPost;
        entity.MatchText = Clean(request.MatchText);

        // The schedule or amount may have changed: drop bills that haven't happened yet and rebuild them.
        var today = DateTime.Today;
        var stale = await db.BillOccurrences
            .Where(b => b.RecurringTransactionId == ruleId && b.Status == BillStatuses.Expected
                && b.TransactionId == null && b.DueDate >= today)
            .ToListAsync(ct);
        db.BillOccurrences.RemoveRange(stale);
        await db.SaveChangesAsync(ct);

        await RefreshBillsAsync(db, entity, ct);
    }

    public async Task DeleteAsync(int groupId, int ruleId, CancellationToken ct = default)
    {
        await using var db = await tenantDb.CreateForGroupAsync(groupId, ct);
        var entity = await db.RecurringTransactions.FirstOrDefaultAsync(r => r.Id == ruleId, ct);
        if (entity is null) return;

        db.RecurringTransactions.Remove(entity); // its bills cascade
        await db.SaveChangesAsync(ct);
    }

    private static async Task RefreshBillsAsync(BBF.Data.ApplicationDbContext db, RecurringTransaction rule, CancellationToken ct)
    {
        if (rule.AutoPost || !rule.IsActive) return;
        var today = DateTime.Today;
        await BillMatching.GenerateForRuleAsync(db, rule, today, ct);
        await db.SaveChangesAsync(ct);
        await BillMatching.MatchAsync(db, today, ct);
        await db.SaveChangesAsync(ct);
    }

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private static RecurringTransactionDto ToDto(RecurringTransaction r) => new(
        r.Id, r.Payee, r.Amount, r.CategoryId, r.Category?.Name,
        r.AccountId, r.Account.Name, r.Frequency, r.NextOccurrence, r.EndDate, r.IsActive,
        r.AutoPost, r.MatchText, r.NextOccurrence);
}
