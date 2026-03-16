using BBF.Data;
using BBF.Data.Entities;
using Going.Plaid;
using Going.Plaid.Accounts;
using Going.Plaid.Entity;
using Going.Plaid.Item;
using Going.Plaid.Link;
using Going.Plaid.Transactions;
using Microsoft.EntityFrameworkCore;

namespace BBF.Services;

public class PlaidService
{
    private readonly PlaidClient _client;
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;

    public PlaidService(IConfiguration config, IDbContextFactory<ApplicationDbContext> dbFactory)
    {
        _dbFactory = dbFactory;

        _redirectUri = config["Plaid:RedirectUri"] ?? "";

        _client = new PlaidClient(
            Going.Plaid.Environment.Production,
            clientId: config["Plaid:ClientId"] ?? "",
            secret: config["Plaid:Secret"] ?? ""
        );
    }

    private readonly string _redirectUri;

    public async Task<string> CreateLinkTokenAsync(string userId)
    {
        var request = new LinkTokenCreateRequest
        {
            User = new LinkTokenCreateRequestUser { ClientUserId = userId },
            ClientName = "BBF",
            Products = [Products.Transactions],
            Language = Going.Plaid.Entity.Language.English,
            CountryCodes = [CountryCode.Us]
        };

        if (!string.IsNullOrEmpty(_redirectUri))
            request.RedirectUri = _redirectUri;

        var response = await _client.LinkTokenCreateAsync(request);

        if (response.Error is not null)
            throw new Exception($"Plaid error: {response.Error.ErrorMessage}");

        return response.LinkToken;
    }

    public async Task<PlaidConnection> ExchangePublicTokenAsync(string publicToken, string institutionName, string institutionId, int? groupId)
    {
        var response = await _client.ItemPublicTokenExchangeAsync(new ItemPublicTokenExchangeRequest
        {
            PublicToken = publicToken
        });

        if (response.Error is not null)
            throw new Exception($"Plaid error: {response.Error.ErrorMessage}");

        var connection = new PlaidConnection
        {
            AccessToken = response.AccessToken,
            ItemId = response.ItemId,
            InstitutionName = institutionName,
            InstitutionId = institutionId,
            GroupId = groupId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        await using var db = await _dbFactory.CreateDbContextAsync();
        db.PlaidConnections.Add(connection);
        await db.SaveChangesAsync();

        // Fetch and store linked accounts
        await SyncAccountsAsync(connection);

        return connection;
    }

    public async Task SyncAccountsAsync(PlaidConnection connection)
    {
        var response = await _client.AccountsGetAsync(new AccountsGetRequest
        {
            AccessToken = connection.AccessToken
        });

        if (response.Error is not null)
            throw new Exception($"Plaid error: {response.Error.ErrorMessage}");

        await using var db = await _dbFactory.CreateDbContextAsync();

        var existingAccounts = await db.PlaidAccounts
            .Where(a => a.PlaidConnectionId == connection.Id)
            .ToListAsync();

        foreach (var account in response.Accounts)
        {
            var existing = existingAccounts.FirstOrDefault(a => a.PlaidAccountId == account.AccountId);
            if (existing is not null)
            {
                // Update metadata but preserve custom name
                existing.Name = account.Name;
                existing.OfficialName = account.OfficialName;
                existing.Mask = account.Mask;
                existing.Type = account.Type.ToString();
                existing.Subtype = account.Subtype?.ToString();
            }
            else
            {
                db.PlaidAccounts.Add(new PlaidAccount
                {
                    PlaidConnectionId = connection.Id,
                    PlaidAccountId = account.AccountId,
                    Name = account.Name,
                    OfficialName = account.OfficialName,
                    Mask = account.Mask,
                    Type = account.Type.ToString(),
                    Subtype = account.Subtype?.ToString(),
                    IsActive = true
                });
            }
        }

        await db.SaveChangesAsync();
    }

    public async Task<int> SyncTransactionsAsync(PlaidConnection connection)
    {
        // SyncTransactionsAsync is long-running with multiple operations - use a single context
        await using var db = await _dbFactory.CreateDbContextAsync();

        // Ensure accounts are populated
        var accountCount = await db.PlaidAccounts.CountAsync(a => a.PlaidConnectionId == connection.Id);
        if (accountCount == 0)
            await SyncAccountsAsync(connection);

        // Build account lookup for display names
        var accounts = await db.PlaidAccounts
            .Where(a => a.PlaidConnectionId == connection.Id)
            .ToDictionaryAsync(a => a.PlaidAccountId, a => a.DisplayName);

        var added = 0;
        var hasMore = true;

        while (hasMore)
        {
            var request = new TransactionsSyncRequest
            {
                AccessToken = connection.AccessToken
            };

            if (!string.IsNullOrEmpty(connection.Cursor))
                request.Cursor = connection.Cursor;

            var response = await _client.TransactionsSyncAsync(request);

            if (response.Error is not null)
                throw new Exception($"Plaid error: {response.Error.ErrorMessage}");

            // Process new transactions
            foreach (var txn in response.Added)
            {
                if (txn.TransactionId is null) continue;

                var exists = await db.Transactions
                    .AnyAsync(t => t.PlaidTransactionId == txn.TransactionId);
                if (exists) continue;

                var category = await MatchCategoryAsync(db, txn, connection.GroupId);
                var source = txn.AccountId is not null && accounts.TryGetValue(txn.AccountId, out var acctName)
                    ? acctName
                    : connection.InstitutionName;

                db.Transactions.Add(new Data.Entities.Transaction
                {
                    PlaidTransactionId = txn.TransactionId,
                    Amount = (decimal)(txn.Amount ?? 0), // Plaid: positive = money out, negative = money in
                    Date = txn.Date?.ToDateTime(TimeOnly.MinValue) ?? DateTime.UtcNow,
                    Description = txn.MerchantName ?? txn.Name ?? txn.OriginalDescription ?? "Unknown",
                    MerchantName = txn.MerchantName,
                    CategoryId = category?.Id,
                    GroupId = connection.GroupId,
                    Source = source,
                    CreatedAt = DateTime.UtcNow
                });
                added++;
            }

            // Handle modified transactions
            foreach (var txn in response.Modified)
            {
                if (txn.TransactionId is null) continue;

                var existing = await db.Transactions
                    .FirstOrDefaultAsync(t => t.PlaidTransactionId == txn.TransactionId);
                if (existing is null) continue;

                existing.Amount = (decimal)(txn.Amount ?? 0);
                existing.Date = txn.Date?.ToDateTime(TimeOnly.MinValue) ?? existing.Date;
                existing.Description = txn.MerchantName ?? txn.Name ?? txn.OriginalDescription ?? existing.Description;
                existing.MerchantName = txn.MerchantName ?? existing.MerchantName;

                if (txn.AccountId is not null && accounts.TryGetValue(txn.AccountId, out var modAcctName))
                    existing.Source = modAcctName;
            }

            // Handle removed transactions
            foreach (var removed in response.Removed)
            {
                if (removed.TransactionId is null) continue;

                var existing = await db.Transactions
                    .FirstOrDefaultAsync(t => t.PlaidTransactionId == removed.TransactionId);
                if (existing is not null)
                    db.Transactions.Remove(existing);
            }

            connection.Cursor = response.NextCursor;
            connection.LastSynced = DateTime.UtcNow;
            hasMore = response.HasMore;
        }

        await db.SaveChangesAsync();
        return added;
    }

    public async Task<List<PlaidConnection>> GetConnectionsAsync(int groupId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.PlaidConnections
            .Include(c => c.Accounts)
            .Where(c => c.IsActive && c.GroupId == groupId)
            .OrderBy(c => c.InstitutionName)
            .ToListAsync();
    }

    public async Task UpdateAccountNameAsync(int accountId, string customName)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var account = await db.PlaidAccounts.FindAsync(accountId);
        if (account is null) return;

        account.CustomName = string.IsNullOrWhiteSpace(customName) ? null : customName.Trim();
        await db.SaveChangesAsync();
    }

    public async Task RemoveConnectionAsync(int connectionId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var connection = await db.PlaidConnections.FindAsync(connectionId);
        if (connection is null) return;

        try
        {
            await _client.ItemRemoveAsync(new ItemRemoveRequest
            {
                AccessToken = connection.AccessToken
            });
        }
        catch { /* Best effort removal from Plaid */ }

        connection.IsActive = false;
        await db.SaveChangesAsync();
    }

    private static async Task<BudgetCategory?> MatchCategoryAsync(ApplicationDbContext db, Going.Plaid.Entity.Transaction txn, int? groupId)
    {
        // Try to match by merchant name against existing categorized transactions within the same group
        if (!string.IsNullOrEmpty(txn.MerchantName))
        {
            var previousMatch = await db.Transactions
                .Where(t => t.MerchantName == txn.MerchantName && t.CategoryId != null && t.GroupId == groupId)
                .OrderByDescending(t => t.Date)
                .FirstOrDefaultAsync();

            if (previousMatch?.CategoryId is not null)
                return await db.BudgetCategories.FindAsync(previousMatch.CategoryId);
        }

        return null;
    }
}
