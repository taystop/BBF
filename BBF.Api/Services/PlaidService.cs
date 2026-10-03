using BBF.Api.Auth;
using BBF.Data;
using BBF.Data.Entities;
using BBF.Shared.Contracts.Plaid;
using Going.Plaid;
using Going.Plaid.Accounts;
using Going.Plaid.Entity;
using Going.Plaid.Item;
using Going.Plaid.Link;
using Going.Plaid.Transactions;
using Microsoft.EntityFrameworkCore;

namespace BBF.Api.Services;

public class PlaidService
{
    private readonly PlaidClient _client;
    private readonly ITenantDbContextFactory _tenantDb;
    private readonly ICurrentUserContext _currentUser;
    private readonly string _redirectUri;

    public PlaidService(IConfiguration config, ITenantDbContextFactory tenantDb, ICurrentUserContext currentUser)
    {
        _tenantDb = tenantDb;
        _currentUser = currentUser;
        _redirectUri = config["Plaid:RedirectUri"] ?? "";

        _client = new PlaidClient(
            Going.Plaid.Environment.Production,
            clientId: config["Plaid:ClientId"] ?? "",
            secret: config["Plaid:Secret"] ?? ""
        );
    }

    public async Task<string> CreateLinkTokenAsync(CancellationToken ct = default)
    {
        var request = new LinkTokenCreateRequest
        {
            User = new LinkTokenCreateRequestUser { ClientUserId = _currentUser.UserId },
            ClientName = "BBF",
            Products = [Products.Transactions],
            Language = Going.Plaid.Entity.Language.English,
            CountryCodes = [CountryCode.Us]
        };

        if (!string.IsNullOrEmpty(_redirectUri))
            request.RedirectUri = _redirectUri;

        var response = await _client.LinkTokenCreateAsync(request);

        if (response.Error is not null)
            throw new InvalidOperationException($"Plaid error: {response.Error.ErrorMessage}");

        return response.LinkToken;
    }

    public async Task<PlaidConnectionDto> ExchangePublicTokenAsync(int groupId, ExchangePublicTokenRequest request, CancellationToken ct = default)
    {
        var response = await _client.ItemPublicTokenExchangeAsync(new ItemPublicTokenExchangeRequest
        {
            PublicToken = request.PublicToken
        });

        if (response.Error is not null)
            throw new InvalidOperationException($"Plaid error: {response.Error.ErrorMessage}");

        var connection = new PlaidConnection
        {
            AccessToken = response.AccessToken,
            ItemId = response.ItemId,
            InstitutionName = request.InstitutionName,
            InstitutionId = request.InstitutionId,
            GroupId = groupId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        await using var db = await _tenantDb.CreateForGroupAsync(groupId, ct);
        db.PlaidConnections.Add(connection);
        await db.SaveChangesAsync(ct);

        await DoSyncAccountsAsync(db, connection, ct);

        var accounts = await db.PlaidAccounts.Where(a => a.PlaidConnectionId == connection.Id).ToListAsync(ct);
        return ToDto(connection, accounts);
    }

    public async Task<List<PlaidConnectionDto>> GetConnectionsAsync(int groupId, CancellationToken ct = default)
    {
        await using var db = await _tenantDb.CreateForGroupAsync(groupId, ct);
        var connections = await db.PlaidConnections
            .Include(c => c.Accounts)
            .Where(c => c.IsActive)
            .OrderBy(c => c.InstitutionName)
            .ToListAsync(ct);

        return connections.Select(c => ToDto(c, c.Accounts.ToList())).ToList();
    }

    public async Task<SyncTransactionsResult> SyncTransactionsAsync(int groupId, int connectionId, CancellationToken ct = default)
    {
        await using var db = await _tenantDb.CreateForGroupAsync(groupId, ct);
        var connection = await db.PlaidConnections.FirstOrDefaultAsync(c => c.Id == connectionId, ct)
            ?? throw new KeyNotFoundException($"Plaid connection {connectionId} not found.");

        var accountCount = await db.PlaidAccounts.CountAsync(a => a.PlaidConnectionId == connection.Id, ct);
        if (accountCount == 0)
            await DoSyncAccountsAsync(db, connection, ct);

        var accounts = await db.PlaidAccounts
            .Where(a => a.PlaidConnectionId == connection.Id)
            .ToDictionaryAsync(a => a.PlaidAccountId, a => a.DisplayName, ct);

        var added = 0;
        var hasMore = true;

        while (hasMore)
        {
            var request = new TransactionsSyncRequest { AccessToken = connection.AccessToken };
            if (!string.IsNullOrEmpty(connection.Cursor))
                request.Cursor = connection.Cursor;

            var response = await _client.TransactionsSyncAsync(request);

            if (response.Error is not null)
                throw new InvalidOperationException($"Plaid error: {response.Error.ErrorMessage}");

            foreach (var txn in response.Added)
            {
                if (txn.TransactionId is null) continue;

                var exists = await db.Transactions.AnyAsync(t => t.PlaidTransactionId == txn.TransactionId, ct);
                if (exists) continue;

                var category = await MatchCategoryAsync(db, txn, ct);
                var source = txn.AccountId is not null && accounts.TryGetValue(txn.AccountId, out var acctName)
                    ? acctName
                    : connection.InstitutionName;

                db.Transactions.Add(new BBF.Data.Entities.Transaction
                {
                    PlaidTransactionId = txn.TransactionId,
                    Amount = (decimal)(txn.Amount ?? 0), // Plaid: positive = money out, negative = money in
                    Date = txn.Date?.ToDateTime(TimeOnly.MinValue) ?? DateTime.UtcNow,
                    Description = txn.MerchantName ?? txn.Name ?? txn.OriginalDescription ?? "Unknown",
                    MerchantName = txn.MerchantName,
                    CategoryId = category?.Id,
                    GroupId = groupId,
                    Source = source,
                    CreatedAt = DateTime.UtcNow
                });
                added++;
            }

            foreach (var txn in response.Modified)
            {
                if (txn.TransactionId is null) continue;

                var existing = await db.Transactions.FirstOrDefaultAsync(t => t.PlaidTransactionId == txn.TransactionId, ct);
                if (existing is null) continue;

                existing.Amount = (decimal)(txn.Amount ?? 0);
                existing.Date = txn.Date?.ToDateTime(TimeOnly.MinValue) ?? existing.Date;
                existing.Description = txn.MerchantName ?? txn.Name ?? txn.OriginalDescription ?? existing.Description;
                existing.MerchantName = txn.MerchantName ?? existing.MerchantName;

                if (txn.AccountId is not null && accounts.TryGetValue(txn.AccountId, out var modAcctName))
                    existing.Source = modAcctName;
            }

            foreach (var removed in response.Removed)
            {
                if (removed.TransactionId is null) continue;

                var existing = await db.Transactions.FirstOrDefaultAsync(t => t.PlaidTransactionId == removed.TransactionId, ct);
                if (existing is not null)
                    db.Transactions.Remove(existing);
            }

            connection.Cursor = response.NextCursor;
            connection.LastSynced = DateTime.UtcNow;
            hasMore = response.HasMore;
        }

        await db.SaveChangesAsync(ct);
        return new SyncTransactionsResult(added);
    }

    public async Task UpdateAccountNameAsync(int groupId, int accountId, UpdatePlaidAccountNameRequest request, CancellationToken ct = default)
    {
        await using var db = await _tenantDb.CreateForGroupAsync(groupId, ct);

        // PlaidAccount carries no GroupId of its own — the required-navigation + filtered-parent trick
        // (see MealPlanWeeksService.UnassignAsync) confirms tenancy: a null Connection means the account
        // either doesn't exist or belongs to another tenant's connection.
        var account = await db.PlaidAccounts.Include(a => a.Connection).FirstOrDefaultAsync(a => a.Id == accountId, ct);
        if (account?.Connection is null) return;

        account.CustomName = string.IsNullOrWhiteSpace(request.CustomName) ? null : request.CustomName.Trim();
        await db.SaveChangesAsync(ct);
    }

    public async Task RemoveConnectionAsync(int groupId, int connectionId, CancellationToken ct = default)
    {
        await using var db = await _tenantDb.CreateForGroupAsync(groupId, ct);
        var connection = await db.PlaidConnections.FirstOrDefaultAsync(c => c.Id == connectionId, ct);
        if (connection is null) return;

        try
        {
            await _client.ItemRemoveAsync(new ItemRemoveRequest { AccessToken = connection.AccessToken });
        }
        catch { /* Best effort removal from Plaid */ }

        connection.IsActive = false;
        await db.SaveChangesAsync(ct);
    }

    private async Task DoSyncAccountsAsync(ApplicationDbContext db, PlaidConnection connection, CancellationToken ct)
    {
        var response = await _client.AccountsGetAsync(new AccountsGetRequest { AccessToken = connection.AccessToken });
        if (response.Error is not null)
            throw new InvalidOperationException($"Plaid error: {response.Error.ErrorMessage}");

        var existingAccounts = await db.PlaidAccounts.Where(a => a.PlaidConnectionId == connection.Id).ToListAsync(ct);

        foreach (var account in response.Accounts)
        {
            var existing = existingAccounts.FirstOrDefault(a => a.PlaidAccountId == account.AccountId);
            if (existing is not null)
            {
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

        await db.SaveChangesAsync(ct);
    }

    private async Task<BudgetCategory?> MatchCategoryAsync(ApplicationDbContext db, Going.Plaid.Entity.Transaction txn, CancellationToken ct)
    {
        // Try to match by merchant name against existing categorized transactions (db is already
        // scoped to the connection's group, via CurrentGroupId — no explicit GroupId check needed).
        if (!string.IsNullOrEmpty(txn.MerchantName))
        {
            var previousMatch = await db.Transactions
                .Where(t => t.MerchantName == txn.MerchantName && t.CategoryId != null)
                .OrderByDescending(t => t.Date)
                .FirstOrDefaultAsync(ct);

            if (previousMatch?.CategoryId is int categoryId)
                return await db.BudgetCategories.FirstOrDefaultAsync(c => c.Id == categoryId, ct);
        }

        return null;
    }

    private static PlaidConnectionDto ToDto(PlaidConnection c, List<PlaidAccount> accounts) => new(
        c.Id, c.InstitutionName, c.InstitutionId, c.LastSynced, c.IsActive,
        accounts.Select(a => new PlaidAccountDto(
            a.Id, a.PlaidAccountId, a.Name, a.OfficialName, a.CustomName, a.Mask, a.Type, a.Subtype, a.IsActive, a.DisplayName)).ToList());
}
