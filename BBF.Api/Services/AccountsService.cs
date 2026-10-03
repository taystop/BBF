using BBF.Api.Auth;
using BBF.Data;
using BBF.Data.Entities;
using BBF.Shared.Contracts.Accounts;
using Microsoft.EntityFrameworkCore;

namespace BBF.Api.Services;

public class AccountsService(ITenantDbContextFactory tenantDb)
{
    public async Task<List<AccountDto>> GetAccountsAsync(int groupId, CancellationToken ct = default)
    {
        await using var db = await tenantDb.CreateForGroupAsync(groupId, ct);
        return await db.Accounts
            .OrderBy(a => a.Name)
            .Select(a => new AccountDto(a.Id, a.Name, a.AccountType, a.StartingBalance, a.CurrentBalance, a.IsActive, a.PlaidAccountId))
            .ToListAsync(ct);
    }

    public async Task<AccountDto> CreateAsync(int groupId, CreateAccountRequest request, CancellationToken ct = default)
    {
        await using var db = await tenantDb.CreateForGroupAsync(groupId, ct);
        var account = new Account
        {
            Name = request.Name,
            AccountType = request.AccountType,
            StartingBalance = request.StartingBalance,
            CurrentBalance = request.StartingBalance,
            GroupId = groupId,
        };
        db.Accounts.Add(account);
        await db.SaveChangesAsync(ct);
        return ToDto(account);
    }

    public async Task UpdateAsync(int groupId, int accountId, UpdateAccountRequest request, CancellationToken ct = default)
    {
        await using var db = await tenantDb.CreateForGroupAsync(groupId, ct);
        var entity = await db.Accounts.FirstOrDefaultAsync(a => a.Id == accountId, ct);
        if (entity is null) return; // the query filter means a cross-tenant id is simply never found

        entity.Name = request.Name;
        entity.AccountType = request.AccountType;
        entity.PlaidAccountId = request.PlaidAccountId;
        await db.SaveChangesAsync(ct);
    }

    public async Task SetActiveAsync(int groupId, int accountId, bool isActive, CancellationToken ct = default)
    {
        await using var db = await tenantDb.CreateForGroupAsync(groupId, ct);
        var entity = await db.Accounts.FirstOrDefaultAsync(a => a.Id == accountId, ct);
        if (entity is null) return;

        entity.IsActive = isActive;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Recomputes CurrentBalance from StartingBalance minus the sum of linked transactions. Takes an
    /// already-open context so it runs in the same unit of work as the write that triggered it — ported
    /// verbatim from the old AccountService.
    /// </summary>
    public static async Task RecalculateBalanceAsync(int accountId, ApplicationDbContext db)
    {
        var account = await db.Accounts.FirstOrDefaultAsync(a => a.Id == accountId);
        if (account is null) return;

        var spent = await db.Transactions
            .Where(t => t.AccountId == accountId)
            .SumAsync(t => t.Amount);

        account.CurrentBalance = account.StartingBalance - spent;
    }

    private static AccountDto ToDto(Account a) =>
        new(a.Id, a.Name, a.AccountType, a.StartingBalance, a.CurrentBalance, a.IsActive, a.PlaidAccountId);
}
