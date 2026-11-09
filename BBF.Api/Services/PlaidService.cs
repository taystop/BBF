using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
using Going.Plaid.WebhookVerificationKey;
using Microsoft.EntityFrameworkCore;

namespace BBF.Api.Services;

/// <summary>
/// Plaid integration. Cost notes (see https://plaid.com/docs/account/billing/): Transactions is billed per
/// connected Item per month, so /transactions/sync and /accounts/get can be called as often as needed at
/// no extra cost. /accounts/balance/get and /transactions/refresh are billed per call — this class never
/// calls either.
/// </summary>
public class PlaidService
{
    public const string CreditCardPaymentsCategory = "Credit card payments";

    // One sync at a time per connection: a webhook, the backup timer and a manual "Sync" press can all
    // land together, and two syncs sharing a cursor would insert the same transactions twice.
    private static readonly ConcurrentDictionary<int, SemaphoreSlim> SyncLocks = new();
    private static readonly ConcurrentDictionary<string, JWKPublicKey> WebhookKeys = new();

    private readonly PlaidClient _client;
    private readonly ITenantDbContextFactory _tenantDb;
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
    private readonly ICurrentUserContext _currentUser;
    private readonly ILogger<PlaidService> _logger;
    private readonly string _redirectUri;
    private readonly string? _webhookUrl;

    public PlaidService(
        IConfiguration config,
        ITenantDbContextFactory tenantDb,
        IDbContextFactory<ApplicationDbContext> dbFactory,
        ICurrentUserContext currentUser,
        ILogger<PlaidService> logger)
    {
        _tenantDb = tenantDb;
        _dbFactory = dbFactory;
        _currentUser = currentUser;
        _logger = logger;
        _redirectUri = config["Plaid:RedirectUri"] ?? "";
        _webhookUrl = string.IsNullOrWhiteSpace(config["Plaid:WebhookUrl"]) ? null : config["Plaid:WebhookUrl"];

        _client = new PlaidClient(
            Going.Plaid.Environment.Production,
            clientId: config["Plaid:ClientId"] ?? "",
            secret: config["Plaid:Secret"] ?? ""
        );
    }

    // ---------------------------------------------------------------- Link

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
        if (_webhookUrl is not null)
            request.Webhook = _webhookUrl;

        var response = await _client.LinkTokenCreateAsync(request);

        if (response.Error is not null)
            throw new InvalidOperationException($"Plaid error: {response.Error.ErrorMessage}");

        return response.LinkToken;
    }

    /// <summary>
    /// Update-mode Link token with account selection, so savings/credit cards at an already-connected
    /// bank join the existing Item (no second monthly fee). Per Plaid's update-mode docs: pass the
    /// access token, leave products empty, and no public-token exchange is needed afterward.
    /// </summary>
    public async Task<string> CreateUpdateLinkTokenAsync(int groupId, int connectionId, CancellationToken ct = default)
    {
        await using var db = await _tenantDb.CreateForGroupAsync(groupId, ct);
        var connection = await db.PlaidConnections.FirstOrDefaultAsync(c => c.Id == connectionId && c.IsActive, ct)
            ?? throw new KeyNotFoundException($"Plaid connection {connectionId} not found.");

        var request = new LinkTokenCreateRequest
        {
            User = new LinkTokenCreateRequestUser { ClientUserId = _currentUser.UserId },
            ClientName = "BBF",
            Language = Going.Plaid.Entity.Language.English,
            CountryCodes = [CountryCode.Us],
            AccessToken = connection.AccessToken,
            Update = new LinkTokenCreateRequestUpdate { AccountSelectionEnabled = true },
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
            WebhookUrl = _webhookUrl, // set on the link token this Item was created from
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

        var userId = _currentUser.UserId;
        var hiddenPlaidIds = (await db.Accounts
                .Where(a => a.PlaidAccountId != null && a.PrivateToUserId != null && a.PrivateToUserId != userId)
                .Select(a => a.PlaidAccountId!.Value)
                .ToListAsync(ct))
            .ToHashSet();

        return connections.Select(c => ToDto(c, c.Accounts.Where(a => !hiddenPlaidIds.Contains(a.Id)).ToList())).ToList();
    }

    public async Task RefreshAccountsAsync(int groupId, int connectionId, CancellationToken ct = default)
    {
        await using var db = await _tenantDb.CreateForGroupAsync(groupId, ct);
        var connection = await db.PlaidConnections.FirstOrDefaultAsync(c => c.Id == connectionId && c.IsActive, ct)
            ?? throw new KeyNotFoundException($"Plaid connection {connectionId} not found.");
        await DoSyncAccountsAsync(db, connection, ct);
    }

    // ---------------------------------------------------------------- Sync

    /// <summary>User-triggered sync ("Sync from Bank"): membership is checked before anything runs.</summary>
    public async Task<SyncTransactionsResult> SyncTransactionsAsync(int groupId, int connectionId, CancellationToken ct = default)
    {
        await using var db = await _tenantDb.CreateForGroupAsync(groupId, ct);
        var connection = await db.PlaidConnections.FirstOrDefaultAsync(c => c.Id == connectionId, ct)
            ?? throw new KeyNotFoundException($"Plaid connection {connectionId} not found.");
        return new SyncTransactionsResult(await SyncConnectionAsync(db, connection, ct));
    }

    /// <summary>
    /// Trusted sync from the webhook/backup job (no signed-in user). The context is scoped to the
    /// connection's own group before any other query runs, so the tenancy filters still apply.
    /// </summary>
    public async Task<int> SyncConnectionAsTrustedAsync(int connectionId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var connection = await db.PlaidConnections.FirstOrDefaultAsync(c => c.Id == connectionId && c.IsActive, ct);
        if (connection?.GroupId is not int groupId) return 0;

        db.CurrentGroupId = groupId;
        return await SyncConnectionAsync(db, connection, ct);
    }

    public async Task<List<int>> GetActiveConnectionIdsAsync(CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await db.PlaidConnections.Where(c => c.IsActive && c.GroupId != null).Select(c => c.Id).ToListAsync(ct);
    }

    public async Task<int?> FindConnectionIdByItemIdAsync(string itemId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await db.PlaidConnections
            .Where(c => c.ItemId == itemId && c.IsActive)
            .Select(c => (int?)c.Id)
            .FirstOrDefaultAsync(ct);
    }

    /// <param name="db">Must already be scoped to <paramref name="connection"/>'s group.</param>
    private async Task<int> SyncConnectionAsync(ApplicationDbContext db, PlaidConnection connection, CancellationToken ct)
    {
        var gate = SyncLocks.GetOrAdd(connection.Id, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            await EnsureWebhookAsync(connection, ct);

            // Free endpoint: refreshes account list + cached balances and links any new accounts.
            await DoSyncAccountsAsync(db, connection, ct);

            var links = await db.PlaidAccounts
                .Where(pa => pa.PlaidConnectionId == connection.Id)
                .Select(pa => new
                {
                    pa.PlaidAccountId,
                    IsCredit = pa.Type == "Credit",
                    AccountId = db.Accounts.Where(a => a.PlaidAccountId == pa.Id).Select(a => (int?)a.Id).FirstOrDefault()
                })
                .ToDictionaryAsync(x => x.PlaidAccountId, ct);

            int? cardPaymentCategoryId = null;
            async Task<int> CardPaymentCategoryAsync() =>
                cardPaymentCategoryId ??= await EnsureCategoryAsync(db, connection.GroupId, CreditCardPaymentsCategory, ct);

            var added = 0;
            var hasMore = true;
            while (hasMore)
            {
                var request = new TransactionsSyncRequest
                {
                    AccessToken = connection.AccessToken,
                    // The bank's raw text tells a paycheck from a refund at the same company. No extra cost.
                    Options = new TransactionsSyncRequestOptions { IncludeOriginalDescription = true },
                };
                if (!string.IsNullOrEmpty(connection.Cursor))
                    request.Cursor = connection.Cursor;

                var response = await _client.TransactionsSyncAsync(request);
                if (response.Error is not null)
                    throw new InvalidOperationException($"Plaid error: {response.Error.ErrorMessage}");

                foreach (var txn in response.Added)
                {
                    if (txn.TransactionId is null) continue;
                    if (await db.Transactions.AnyAsync(t => t.PlaidTransactionId == txn.TransactionId, ct)) continue;

                    links.TryGetValue(txn.AccountId ?? "", out var link);
                    var amount = (decimal)(txn.Amount ?? 0); // Plaid: positive = money out, negative = money in
                    var detailed = txn.PersonalFinanceCategory?.Detailed;
                    var bankText = txn.OriginalDescription ?? txn.Name;
                    var (kind, isCardPayment) = Classify(txn.PersonalFinanceCategory?.Primary, detailed, amount, link?.IsCredit == true, bankText);

                    // A posted transaction replacing a pending one: update the pending row in place so its
                    // category, kind and any paid-bill link carry over. Plaid's matching "removed" entry
                    // for the old pending id then finds nothing to delete.
                    var pending = txn.PendingTransactionId is null ? null
                        : await db.Transactions.FirstOrDefaultAsync(t => t.PlaidTransactionId == txn.PendingTransactionId, ct);
                    var row = pending ?? new BBF.Data.Entities.Transaction
                    {
                        GroupId = connection.GroupId,
                        Source = "Plaid",
                        CreatedAt = DateTime.UtcNow,
                    };

                    row.PlaidTransactionId = txn.TransactionId;
                    row.Amount = amount;
                    row.Date = txn.Date?.ToDateTime(TimeOnly.MinValue) ?? DateTime.UtcNow.Date;
                    row.Description = txn.MerchantName ?? txn.Name ?? txn.OriginalDescription ?? "Unknown";
                    row.MerchantName = txn.MerchantName;
                    row.BankDescription = bankText;
                    row.AccountId = link?.AccountId;
                    row.IsPending = txn.Pending == true;
                    row.PlaidCategory = detailed;
                    if (!row.KindSetManually)
                    {
                        row.Kind = kind;
                        // A pending deposit may have picked up a store category before it was recognised as pay.
                        if (kind == TransactionKinds.Income && amount < 0)
                            row.CategoryId = null;
                    }

                    if (row.CategoryId is null && row.Kind == TransactionKinds.Spending)
                    {
                        row.CategoryId = isCardPayment
                            ? await CardPaymentCategoryAsync()
                            : (await MatchCategoryAsync(db, txn, ct))?.Id;
                    }

                    if (pending is null)
                    {
                        db.Transactions.Add(row);
                        added++;
                    }
                }

                foreach (var txn in response.Modified)
                {
                    if (txn.TransactionId is null) continue;

                    var existing = await db.Transactions.FirstOrDefaultAsync(t => t.PlaidTransactionId == txn.TransactionId, ct);
                    if (existing is null) continue;

                    links.TryGetValue(txn.AccountId ?? "", out var link);
                    existing.Amount = (decimal)(txn.Amount ?? 0);
                    existing.Date = txn.Date?.ToDateTime(TimeOnly.MinValue) ?? existing.Date;
                    existing.Description = txn.MerchantName ?? txn.Name ?? txn.OriginalDescription ?? existing.Description;
                    existing.MerchantName = txn.MerchantName ?? existing.MerchantName;
                    existing.BankDescription = txn.OriginalDescription ?? txn.Name ?? existing.BankDescription;
                    existing.IsPending = txn.Pending == true;
                    existing.PlaidCategory = txn.PersonalFinanceCategory?.Detailed ?? existing.PlaidCategory;
                    existing.AccountId = link?.AccountId ?? existing.AccountId;
                    if (!existing.KindSetManually)
                    {
                        existing.Kind = Classify(txn.PersonalFinanceCategory?.Primary, existing.PlaidCategory, existing.Amount,
                            link?.IsCredit == true, existing.BankDescription).Kind;
                        if (existing.Kind == TransactionKinds.Income && existing.Amount < 0)
                            existing.CategoryId = null; // a paycheck isn't a grocery refund
                    }
                }

                // Save before removals: a pending row converted above still has its old Plaid id in the
                // database until saved, and the removal lookup below would otherwise delete it.
                await db.SaveChangesAsync(ct);

                var removedIds = new List<int>();
                foreach (var removed in response.Removed)
                {
                    if (removed.TransactionId is null) continue;

                    var existing = await db.Transactions.FirstOrDefaultAsync(t => t.PlaidTransactionId == removed.TransactionId, ct);
                    if (existing is null) continue;
                    removedIds.Add(existing.Id);
                    db.Transactions.Remove(existing);
                }
                await BillMatching.UnlinkTransactionsAsync(db, removedIds, ct);
                await PaycheckMatching.UnlinkTransactionsAsync(db, removedIds, ct);

                connection.Cursor = response.NextCursor;
                connection.LastSynced = DateTime.UtcNow;
                hasMore = response.HasMore;
            }

            await db.SaveChangesAsync(ct);

            await PairTransfersAsync(db, ct);
            await PaycheckMatching.MatchAsync(db, DateTime.Today, ct);
            await db.SaveChangesAsync(ct);
            await BillMatching.MatchAsync(db, DateTime.Today, ct);
            await db.SaveChangesAsync(ct);

            return added;
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// Sorts a Plaid transaction into Spending / Income / Transfer from its personal finance category
    /// (taxonomy: https://plaid.com/documents/transactions-personal-finance-category-taxonomy.csv).
    /// Credit card payments are Spending on the checking side (the household budgets the payment) and a
    /// Transfer on the card side. Generic account transfers are left as Spending/Income until
    /// <see cref="PairTransfersAsync"/> finds the other half in one of the group's own accounts — a
    /// Venmo/Zelle to someone else shouldn't vanish from the budget.
    /// </summary>
    private static (string Kind, bool IsCardPayment) Classify(string? primary, string? detailed, decimal amount, bool isCreditAccount, string? bankText)
    {
        // Payroll from an employer you also shop at (e.g. Walmart) can arrive tagged as a merchant
        // transaction; the bank's own text still says it's pay.
        if (amount < 0 && !isCreditAccount && LooksLikePayroll(bankText))
            return (TransactionKinds.Income, false);

        if (detailed == "LOAN_PAYMENTS_CREDIT_CARD_PAYMENT")
            return isCreditAccount ? (TransactionKinds.Transfer, false) : (TransactionKinds.Spending, true);

        if (isCreditAccount && amount < 0 && primary == "TRANSFER_IN")
            return (TransactionKinds.Transfer, false); // payment received on the card

        if (detailed is "TRANSFER_IN_SAVINGS" or "TRANSFER_OUT_SAVINGS"
            or "TRANSFER_IN_INVESTMENT_AND_RETIREMENT_FUNDS" or "TRANSFER_OUT_INVESTMENT_AND_RETIREMENT_FUNDS")
            return (TransactionKinds.Transfer, false);

        if (primary is "INCOME" or "TRANSFER_IN")
            return (TransactionKinds.Income, false);

        return (TransactionKinds.Spending, false);
    }

    private static readonly string[] PayrollWords = ["payroll", "dirdep", "directdep", "salary", "paycheck", "payrl"];

    private static bool LooksLikePayroll(string? bankText)
    {
        var text = BillMatching.Normalize(bankText);
        return text.Length > 0 && PayrollWords.Any(text.Contains);
    }

    /// <summary>
    /// Marks matching money-out / money-in pairs between two of the group's own accounts (same amount,
    /// within 4 days, both tagged as transfers by Plaid) as Transfers so neither side counts.
    /// Never touches a transaction someone has classified by hand.
    /// </summary>
    private static async Task PairTransfersAsync(ApplicationDbContext db, CancellationToken ct)
    {
        var since = DateTime.Today.AddDays(-45);
        var candidates = await db.Transactions
            .Where(t => t.Date >= since && t.AccountId != null && !t.KindSetManually
                && t.Kind != TransactionKinds.Transfer
                && t.PlaidCategory != null && t.PlaidCategory.StartsWith("TRANSFER_"))
            .ToListAsync(ct);

        var outs = candidates.Where(t => t.Amount > 0 && t.PlaidCategory!.StartsWith("TRANSFER_OUT")).ToList();
        var ins = candidates.Where(t => t.Amount < 0 && t.PlaidCategory!.StartsWith("TRANSFER_IN")).ToList();

        foreach (var o in outs)
        {
            var match = ins.FirstOrDefault(i => i.AccountId != o.AccountId
                && i.Amount == -o.Amount
                && Math.Abs((i.Date - o.Date).TotalDays) <= 4);
            if (match is null) continue;

            ins.Remove(match);
            o.Kind = TransactionKinds.Transfer;
            match.Kind = TransactionKinds.Transfer;
        }
    }

    private async Task EnsureWebhookAsync(PlaidConnection connection, CancellationToken ct)
    {
        if (_webhookUrl is null || connection.WebhookUrl == _webhookUrl) return;
        try
        {
            var response = await _client.ItemWebhookUpdateAsync(new ItemWebhookUpdateRequest
            {
                AccessToken = connection.AccessToken,
                Webhook = _webhookUrl,
            });
            if (response.Error is not null)
            {
                _logger.LogWarning("Plaid webhook update failed for connection {ConnectionId}: {Error}", connection.Id, response.Error.ErrorMessage);
                return;
            }
            connection.WebhookUrl = _webhookUrl;
        }
        catch (Exception ex)
        {
            // Never block a sync on this — the backup timer keeps data flowing either way.
            _logger.LogWarning(ex, "Plaid webhook update failed for connection {ConnectionId}", connection.Id);
        }
    }

    private static async Task<int> EnsureCategoryAsync(ApplicationDbContext db, int? groupId, string name, CancellationToken ct)
    {
        var existing = await db.BudgetCategories.FirstOrDefaultAsync(c => c.Name == name && c.IsActive, ct);
        if (existing is not null) return existing.Id;

        var category = new BudgetCategory
        {
            GroupId = groupId,
            Name = name,
            MonthlyLimit = 0,
            Color = "#78909c",
            Icon = "CreditCard",
            SortOrder = 900,
        };
        db.BudgetCategories.Add(category);
        await db.SaveChangesAsync(ct);
        return category.Id;
    }

    // ---------------------------------------------------------------- Webhook

    /// <summary>
    /// Verifies a Plaid webhook per https://plaid.com/docs/api/webhooks/webhook-verification/: ES256 JWT
    /// in the Plaid-Verification header, signed by the key Plaid returns for its kid, issued within the
    /// last 5 minutes, whose request_body_sha256 claim matches the raw body.
    /// </summary>
    public async Task<bool> VerifyWebhookAsync(string? jwt, byte[] body, CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(jwt)) return false;
            var parts = jwt.Split('.');
            if (parts.Length != 3) return false;

            using var header = JsonDocument.Parse(Base64UrlDecode(parts[0]));
            if (header.RootElement.GetProperty("alg").GetString() != "ES256") return false;
            var kid = header.RootElement.GetProperty("kid").GetString();
            if (string.IsNullOrEmpty(kid)) return false;

            if (!WebhookKeys.TryGetValue(kid, out var key))
            {
                var response = await _client.WebhookVerificationKeyGetAsync(new WebhookVerificationKeyGetRequest { KeyId = kid });
                if (response.Error is not null || response.Key is null) return false;
                key = response.Key;
                WebhookKeys[kid] = key;
            }
            if (key.ExpiredAt is not null) return false;

            using var ecdsa = ECDsa.Create(new ECParameters
            {
                Curve = ECCurve.NamedCurves.nistP256,
                Q = new ECPoint { X = Base64UrlDecode(key.X), Y = Base64UrlDecode(key.Y) },
            });
            var signedBytes = Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}");
            if (!ecdsa.VerifyData(signedBytes, Base64UrlDecode(parts[2]), HashAlgorithmName.SHA256)) return false;

            using var payload = JsonDocument.Parse(Base64UrlDecode(parts[1]));
            var issuedAt = DateTimeOffset.FromUnixTimeSeconds(payload.RootElement.GetProperty("iat").GetInt64());
            if (DateTimeOffset.UtcNow - issuedAt > TimeSpan.FromMinutes(5)) return false;

            var claimedHash = payload.RootElement.GetProperty("request_body_sha256").GetString() ?? "";
            var actualHash = Convert.ToHexStringLower(SHA256.HashData(body));
            return CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(actualHash), Encoding.ASCII.GetBytes(claimedHash.ToLowerInvariant()));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Plaid webhook verification threw");
            return false;
        }
    }

    private static byte[] Base64UrlDecode(string input)
    {
        var s = input.Replace('-', '+').Replace('_', '/');
        s = s.PadRight(s.Length + (4 - s.Length % 4) % 4, '=');
        return Convert.FromBase64String(s);
    }

    // ---------------------------------------------------------------- Accounts

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

    /// <summary>
    /// Refreshes the connection's Plaid accounts and cached balances via /accounts/get (free), creates a
    /// BBF account for any Plaid account not yet linked to one, and copies bank balances onto linked
    /// accounts. Plaid reports a credit card's balance as the positive amount owed; BBF stores it negative
    /// to match how manual card balances are calculated.
    /// </summary>
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
                existing = new PlaidAccount
                {
                    PlaidConnectionId = connection.Id,
                    PlaidAccountId = account.AccountId,
                    Name = account.Name,
                    OfficialName = account.OfficialName,
                    Mask = account.Mask,
                    Type = account.Type.ToString(),
                    Subtype = account.Subtype?.ToString(),
                    IsActive = true
                };
                db.PlaidAccounts.Add(existing);
                existingAccounts.Add(existing);
            }
        }

        await db.SaveChangesAsync(ct);

        foreach (var account in response.Accounts)
        {
            var plaidAccount = existingAccounts.First(a => a.PlaidAccountId == account.AccountId);
            if (!plaidAccount.IsActive) continue;

            var isCredit = plaidAccount.Type == "Credit";
            var bbfAccount = await db.Accounts.FirstOrDefaultAsync(a => a.PlaidAccountId == plaidAccount.Id, ct);
            if (bbfAccount is null)
            {
                var accountType = MapAccountType(plaidAccount);
                bbfAccount = new BBF.Data.Entities.Account
                {
                    GroupId = connection.GroupId,
                    Name = plaidAccount.DisplayName,
                    AccountType = accountType,
                    CountPurchasesInBudget = AccountsService.DefaultCountPurchases(accountType),
                    PlaidAccountId = plaidAccount.Id,
                };
                db.Accounts.Add(bbfAccount);
            }

            if (account.Balances?.Current is { } current)
            {
                bbfAccount.CurrentBalance = isCredit ? -(decimal)current : (decimal)current;
                bbfAccount.AvailableBalance = account.Balances.Available is { } available ? (decimal)available : null;
                bbfAccount.BalanceAsOf = DateTime.UtcNow;
            }
        }

        await db.SaveChangesAsync(ct);
    }

    private static string MapAccountType(PlaidAccount pa) => (pa.Type, pa.Subtype) switch
    {
        ("Credit", _) => "CreditCard",
        ("Investment", _) => "Investment",
        ("Depository", "Savings") or ("Depository", "MoneyMarket") or ("Depository", "Cd") => "Savings",
        _ => "Checking",
    };

    private async Task<BudgetCategory?> MatchCategoryAsync(ApplicationDbContext db, Going.Plaid.Entity.Transaction txn, CancellationToken ct)
    {
        // Try to match by merchant name against existing categorized transactions (db is already
        // scoped to the connection's group, via CurrentGroupId — no explicit GroupId check needed).
        if (!string.IsNullOrEmpty(txn.MerchantName))
        {
            var previousMatch = await db.Transactions
                .Where(t => t.MerchantName == txn.MerchantName && t.CategoryId != null && t.Kind == TransactionKinds.Spending)
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
