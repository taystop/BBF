using BBF.Data;
using BBF.Data.Entities;
using Going.Plaid;
using Going.Plaid.Entity;
using Going.Plaid.Item;
using Going.Plaid.Link;
using Going.Plaid.Transactions;
using Microsoft.EntityFrameworkCore;

namespace BBF.Services;

public class PlaidService
{
    private readonly PlaidClient _client;
    private readonly ApplicationDbContext _db;

    public PlaidService(IConfiguration config, ApplicationDbContext db)
    {
        _db = db;

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

    public async Task<PlaidConnection> ExchangePublicTokenAsync(string publicToken, string institutionName, string institutionId)
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
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.PlaidConnections.Add(connection);
        await _db.SaveChangesAsync();
        return connection;
    }

    public async Task<int> SyncTransactionsAsync(PlaidConnection connection)
    {
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

                var exists = await _db.Transactions
                    .AnyAsync(t => t.PlaidTransactionId == txn.TransactionId);
                if (exists) continue;

                var category = await MatchCategoryAsync(txn);

                _db.Transactions.Add(new Data.Entities.Transaction
                {
                    PlaidTransactionId = txn.TransactionId,
                    Amount = (decimal)(txn.Amount ?? 0) * -1, // Plaid uses negative for income
                    Date = txn.Date?.ToDateTime(TimeOnly.MinValue) ?? DateTime.UtcNow,
                    Description = txn.MerchantName ?? txn.OriginalDescription ?? "Unknown",
                    MerchantName = txn.MerchantName,
                    CategoryId = category?.Id,
                    Source = "Plaid",
                    CreatedAt = DateTime.UtcNow
                });
                added++;
            }

            // Handle modified transactions
            foreach (var txn in response.Modified)
            {
                if (txn.TransactionId is null) continue;

                var existing = await _db.Transactions
                    .FirstOrDefaultAsync(t => t.PlaidTransactionId == txn.TransactionId);
                if (existing is null) continue;

                existing.Amount = (decimal)(txn.Amount ?? 0) * -1;
                existing.Date = txn.Date?.ToDateTime(TimeOnly.MinValue) ?? existing.Date;
                existing.Description = txn.MerchantName ?? txn.OriginalDescription ?? existing.Description;
                existing.MerchantName = txn.MerchantName ?? existing.MerchantName;
            }

            // Handle removed transactions
            foreach (var removed in response.Removed)
            {
                if (removed.TransactionId is null) continue;

                var existing = await _db.Transactions
                    .FirstOrDefaultAsync(t => t.PlaidTransactionId == removed.TransactionId);
                if (existing is not null)
                    _db.Transactions.Remove(existing);
            }

            connection.Cursor = response.NextCursor;
            connection.LastSynced = DateTime.UtcNow;
            hasMore = response.HasMore;
        }

        await _db.SaveChangesAsync();
        return added;
    }

    public async Task<List<PlaidConnection>> GetConnectionsAsync()
    {
        return await _db.PlaidConnections
            .Where(c => c.IsActive)
            .OrderBy(c => c.InstitutionName)
            .ToListAsync();
    }

    public async Task RemoveConnectionAsync(int connectionId)
    {
        var connection = await _db.PlaidConnections.FindAsync(connectionId);
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
        await _db.SaveChangesAsync();
    }

    private async Task<BudgetCategory?> MatchCategoryAsync(Going.Plaid.Entity.Transaction txn)
    {
        // Try to match by merchant name against existing categorized transactions
        if (!string.IsNullOrEmpty(txn.MerchantName))
        {
            var previousMatch = await _db.Transactions
                .Where(t => t.MerchantName == txn.MerchantName && t.CategoryId != null)
                .OrderByDescending(t => t.Date)
                .FirstOrDefaultAsync();

            if (previousMatch?.CategoryId is not null)
                return await _db.BudgetCategories.FindAsync(previousMatch.CategoryId);
        }

        return null;
    }
}
