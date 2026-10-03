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
        return await db.RecurringTransactions
            .Include(r => r.Category)
            .Include(r => r.Account)
            .OrderBy(r => r.NextOccurrence)
            .Select(r => new RecurringTransactionDto(
                r.Id, r.Payee, r.Amount, r.CategoryId, r.Category != null ? r.Category.Name : null,
                r.AccountId, r.Account.Name, r.Frequency, r.NextOccurrence, r.EndDate, r.IsActive))
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
            NextOccurrence = request.NextOccurrence,
            EndDate = request.EndDate,
            IsActive = true,
        };
        db.RecurringTransactions.Add(rule);
        await db.SaveChangesAsync(ct);

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
        entity.NextOccurrence = request.NextOccurrence;
        entity.EndDate = request.EndDate;
        entity.IsActive = request.IsActive;
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int groupId, int ruleId, CancellationToken ct = default)
    {
        await using var db = await tenantDb.CreateForGroupAsync(groupId, ct);
        var entity = await db.RecurringTransactions.FirstOrDefaultAsync(r => r.Id == ruleId, ct);
        if (entity is null) return;

        db.RecurringTransactions.Remove(entity);
        await db.SaveChangesAsync(ct);
    }

    private static RecurringTransactionDto ToDto(RecurringTransaction r) => new(
        r.Id, r.Payee, r.Amount, r.CategoryId, r.Category?.Name,
        r.AccountId, r.Account.Name, r.Frequency, r.NextOccurrence, r.EndDate, r.IsActive);
}
