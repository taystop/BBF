using BBF.Data;
using BBF.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace BBF.Services;

public class AccountService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;

    public AccountService(IDbContextFactory<ApplicationDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<List<Account>> GetAccountsAsync(int groupId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.Accounts
            .Where(a => a.GroupId == groupId)
            .OrderBy(a => a.Name)
            .ToListAsync();
    }

    public async Task<Account> CreateAsync(Account account)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        account.CurrentBalance = account.StartingBalance;
        db.Accounts.Add(account);
        await db.SaveChangesAsync();
        return account;
    }

    public async Task UpdateAsync(int accountId, string name, string accountType, int? plaidAccountId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var entity = await db.Accounts.FindAsync(accountId);
        if (entity is null) return;

        entity.Name = name;
        entity.AccountType = accountType;
        entity.PlaidAccountId = plaidAccountId;
        await db.SaveChangesAsync();
    }

    public async Task SetActiveAsync(int accountId, bool isActive)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var entity = await db.Accounts.FindAsync(accountId);
        if (entity is null) return;

        entity.IsActive = isActive;
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Recomputes CurrentBalance from StartingBalance minus the sum of linked transactions.
    /// Takes an already-open context so it runs in the same unit of work as the write that triggered it.
    /// </summary>
    public static async Task RecalculateBalanceAsync(int accountId, ApplicationDbContext db)
    {
        var account = await db.Accounts.FindAsync(accountId);
        if (account is null) return;

        var spent = await db.Transactions
            .Where(t => t.AccountId == accountId)
            .SumAsync(t => t.Amount);

        account.CurrentBalance = account.StartingBalance - spent;
    }
}
