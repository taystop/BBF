using BBF.Api.Auth;
using BBF.Data.Entities;
using BBF.Shared.Contracts.Bills;
using Microsoft.EntityFrameworkCore;

namespace BBF.Api.Services;

public class BillsService(ITenantDbContextFactory tenantDb, ICurrentUserContext currentUser)
{
    public async Task<List<BillOccurrenceDto>> GetBillsAsync(int groupId, DateTime from, DateTime to, CancellationToken ct = default)
    {
        await using var db = await tenantDb.CreateForGroupAsync(groupId, ct);
        var userId = currentUser.UserId;
        return await db.BillOccurrences
            .Where(b => b.DueDate >= from.Date && b.DueDate <= to.Date)
            .Where(b => b.RecurringTransaction.Account.PrivateToUserId == null || b.RecurringTransaction.Account.PrivateToUserId == userId)
            .OrderBy(b => b.DueDate).ThenBy(b => b.RecurringTransaction.Payee)
            .Select(b => new BillOccurrenceDto(
                b.Id, b.RecurringTransactionId, b.RecurringTransaction.Payee,
                b.RecurringTransaction.Category != null ? b.RecurringTransaction.Category.Name : null,
                b.RecurringTransaction.Account.Name,
                b.DueDate, b.ExpectedAmount, b.Status, b.TransactionId,
                b.Transaction != null ? b.Transaction.Description : null,
                b.Transaction != null ? b.Transaction.Amount : null,
                b.Transaction != null ? b.Transaction.Date : null))
            .ToListAsync(ct);
    }

    public async Task UpdateStatusAsync(int groupId, int billId, UpdateBillStatusRequest request, CancellationToken ct = default)
    {
        if (!BillStatuses.IsValid(request.Status))
            throw new ArgumentException($"Unknown bill status '{request.Status}'.");

        await using var db = await tenantDb.CreateForGroupAsync(groupId, ct);
        var bill = await FindVisibleAsync(db, billId, ct);
        if (bill is null) return;

        bill.Status = request.Status;
        if (request.Status == BillStatuses.Expected)
        {
            bill.TransactionId = null;
            bill.PaidAt = null;
        }
        else if (request.Status == BillStatuses.Paid)
        {
            bill.PaidAt ??= DateTime.UtcNow;
        }
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Moves a bill to the pay period starting on <paramref name="payDate"/>; null restores the default.</summary>
    public async Task MoveToPayDateAsync(int groupId, int billId, DateTime? payDate, CancellationToken ct = default)
    {
        await using var db = await tenantDb.CreateForGroupAsync(groupId, ct);
        var bill = await FindVisibleAsync(db, billId, ct);
        if (bill is null) return;

        bill.AssignedPayDate = payDate?.Date;
        await db.SaveChangesAsync(ct);
    }

    private async Task<BillOccurrence?> FindVisibleAsync(BBF.Data.ApplicationDbContext db, int billId, CancellationToken ct)
    {
        var userId = currentUser.UserId;
        return await db.BillOccurrences
            .Where(b => b.RecurringTransaction.Account.PrivateToUserId == null || b.RecurringTransaction.Account.PrivateToUserId == userId)
            .FirstOrDefaultAsync(b => b.Id == billId, ct);
    }
}
