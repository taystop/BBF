using BBF.Api.Auth;
using BBF.Data;
using BBF.Data.Entities;
using BBF.Shared.Contracts.Money;
using BBF.Shared.Contracts.Transactions;
using Microsoft.EntityFrameworkCore;

namespace BBF.Api.Services;

/// <summary>
/// The daily screens: Money home (safe to spend), the balance forecast, quick categorizing with store
/// rules, and recurring-charge suggestions. Safe to spend and the forecast use checking accounts only.
/// </summary>
public class MoneyService(ITenantDbContextFactory tenantDb, ICurrentUserContext currentUser, PaychecksService paychecks)
{
    public const decimal DefaultLowBalance = 200m;
    private const int SpendingLookbackDays = 28;

    // ------------------------------------------------------------------ Money home

    public async Task<MoneySummaryDto> GetSummaryAsync(int groupId, CancellationToken ct = default)
    {
        var today = DateTime.Today;
        var period = await paychecks.GetPeriodAsync(groupId, today, ct);

        await using var db = await tenantDb.CreateForGroupAsync(groupId, ct);
        var userId = currentUser.UserId;
        var (checking, asOf, hasChecking) = await CheckingBalanceAsync(db, ct);

        var nextPayday = await db.Paychecks
            .Where(p => p.PayDate > today && p.Status != PaycheckStatuses.Skipped)
            .OrderBy(p => p.PayDate)
            .Select(p => (DateTime?)p.PayDate)
            .FirstOrDefaultAsync(ct);

        var nextBills = (await db.BillOccurrences
                .Include(b => b.RecurringTransaction).ThenInclude(r => r.Account)
                .Where(b => b.Status == BillStatuses.Expected && b.DueDate >= today && b.ExpectedAmount > 0
                    && b.RecurringTransaction.Account.CountPurchasesInBudget)
                .OrderBy(b => b.DueDate)
                .Take(5)
                .ToListAsync(ct))
            .Select(b =>
            {
                var hidden = Privacy.IsHiddenFrom(b.RecurringTransaction.Account.PrivateToUserId, userId);
                return new UpcomingBillDto(b.Id, hidden ? Privacy.HiddenBillName : b.RecurringTransaction.Payee,
                    b.DueDate, hidden ? null : b.ExpectedAmount, hidden);
            })
            .ToList();

        var toCategorize = await NeedsCategory(db, userId, await FinanceSettingsService.GetBudgetStartAsync(db, ct)).CountAsync(ct);
        var leftoverPrompt = await AlertsService.GetLeftoverPromptAsync(db, period.Start, ct);
        var alertSince = DateTime.UtcNow.AddDays(-30);
        var openAlerts = await db.FinanceAlerts.CountAsync(a => a.UserId == userId && a.DismissedAt == null && a.CreatedAt >= alertSince, ct);

        return new MoneySummaryDto(
            checking + period.IncomeStillExpected - period.BillsStillDue,
            checking, period.IncomeStillExpected, period.BillsStillDue,
            hasChecking, asOf, nextPayday, period, nextBills, toCategorize,
            leftoverPrompt, openAlerts);
    }

    // ------------------------------------------------------------------ Forecast

    public async Task<ForecastDto> GetForecastAsync(int groupId, int days, CancellationToken ct = default)
    {
        await using var db = await tenantDb.CreateForGroupAsync(groupId, ct);
        return await BuildForecastAsync(db, currentUser.UserId, days, ct);
    }

    /// <summary>The forecast through a group-scoped context; <paramref name="userId"/> is who's looking.</summary>
    public static async Task<ForecastDto> BuildForecastAsync(ApplicationDbContext db, string userId, int days, CancellationToken ct)
    {
        days = Math.Clamp(days, 1, 90);
        var today = DateTime.Today;
        var end = today.AddDays(days - 1);
        var (balance, _, _) = await CheckingBalanceAsync(db, ct);
        var threshold = await db.FinanceSettings.Select(f => f.LowBalanceThreshold).FirstOrDefaultAsync(ct) ?? DefaultLowBalance;

        // Everyday spending from checking that isn't a scheduled bill: the last four weeks, per day.
        var since = today.AddDays(-SpendingLookbackDays);
        var billTransactionIds = db.BillOccurrences.Where(b => b.TransactionId != null).Select(b => b.TransactionId);
        var recentSpending = await db.Transactions
            .Where(t => t.Kind == TransactionKinds.Spending && t.Date >= since && t.Date < today
                && t.Account != null && t.Account.AccountType == "Checking"
                && !billTransactionIds.Contains(t.Id))
            .SumAsync(t => (decimal?)t.Amount, ct) ?? 0m;
        var daily = Math.Round(Math.Max(0, recentSpending) / SpendingLookbackDays, 2);

        var paycheckRows = await db.Paychecks
            .Include(p => p.PaySchedule).ThenInclude(s => s.DepositAccount)
            .Where(p => p.Status == PaycheckStatuses.Expected && p.PayDate >= today && p.PayDate <= end
                && (p.PaySchedule.DepositAccount == null || p.PaySchedule.DepositAccount.AccountType == "Checking"))
            .ToListAsync(ct);

        // Unpaid bills from checking; ones a few days overdue are expected to come out today.
        var billRows = await db.BillOccurrences
            .Include(b => b.RecurringTransaction).ThenInclude(r => r.Account)
            .Where(b => b.Status == BillStatuses.Expected && b.ExpectedAmount > 0
                && b.DueDate >= today.AddDays(-7) && b.DueDate <= end
                && b.RecurringTransaction.Account.AccountType == "Checking")
            .ToListAsync(ct);

        var items = new Dictionary<DateTime, List<(ForecastItemDto Item, decimal Amount)>>();
        void Add(DateTime date, ForecastItemDto item, decimal amount)
        {
            if (!items.TryGetValue(date, out var list)) items[date] = list = [];
            list.Add((item, amount));
        }

        foreach (var p in paycheckRows)
        {
            var hidden = Privacy.IsHiddenFrom(p.PaySchedule, userId);
            Add(p.PayDate, new ForecastItemDto("Paycheck", hidden ? Privacy.HiddenPaycheckName : p.PaySchedule.Name,
                hidden ? null : p.ExpectedAmount, hidden), p.ExpectedAmount);
        }
        foreach (var b in billRows)
        {
            var hidden = Privacy.IsHiddenFrom(b.RecurringTransaction.Account.PrivateToUserId, userId);
            var date = b.DueDate < today ? today : b.DueDate;
            Add(date, new ForecastItemDto("Bill", hidden ? Privacy.HiddenBillName : b.RecurringTransaction.Payee,
                hidden ? null : -b.ExpectedAmount, hidden), -b.ExpectedAmount);
        }

        var startBalance = balance;
        var result = new List<ForecastDayDto>();
        for (var date = today; date <= end; date = date.AddDays(1))
        {
            var dayItems = items.GetValueOrDefault(date) ?? [];
            balance += dayItems.Sum(i => i.Amount);
            if (date > today) balance -= daily;
            result.Add(new ForecastDayDto(date,
                dayItems.OrderByDescending(i => i.Amount).Select(i => i.Item).ToList(),
                Math.Round(balance, 2), balance < threshold));
        }

        return new ForecastDto(Math.Round(startBalance, 2), daily, threshold, result);
    }

    // ------------------------------------------------------------------ Quick review

    public async Task<List<TransactionDto>> GetToReviewAsync(int groupId, CancellationToken ct = default)
    {
        await using var db = await tenantDb.CreateForGroupAsync(groupId, ct);
        var budgetStart = await FinanceSettingsService.GetBudgetStartAsync(db, ct);
        return await NeedsCategory(db, currentUser.UserId, budgetStart)
            .OrderByDescending(t => t.Date).ThenByDescending(t => t.Id)
            .Take(200)
            .Select(t => new TransactionDto(
                t.Id, t.Amount, t.Date, t.Description, t.MerchantName, t.Source,
                t.CategoryId, null, null,
                t.AccountId, t.Account != null ? t.Account.Name : null, t.RecurringTransactionId,
                t.Kind, t.IsPending, t.PlaidCategory))
            .ToListAsync(ct);
    }

    public async Task<CategorizeResult> CategorizeAsync(int groupId, int transactionId, CategorizeRequest request, CancellationToken ct = default)
    {
        await using var db = await tenantDb.CreateForGroupAsync(groupId, ct);
        var userId = currentUser.UserId;

        var transaction = await db.Transactions.VisibleTo(db, userId).FirstOrDefaultAsync(t => t.Id == transactionId, ct)
            ?? throw new KeyNotFoundException("Transaction not found.");
        var category = await db.BudgetCategories.FirstOrDefaultAsync(c => c.Id == request.CategoryId, ct)
            ?? throw new ArgumentException("Category not found.");

        transaction.CategoryId = category.Id;
        await db.SaveChangesAsync(ct);
        if (!request.RememberMerchant) return new CategorizeResult(0, null);

        var display = (transaction.MerchantName ?? transaction.Description).Trim();
        var matchText = BillMatching.Normalize(display);
        if (matchText.Length < 3) return new CategorizeResult(0, null);

        var rule = await db.MerchantRules.FirstOrDefaultAsync(r => r.MatchText == matchText, ct);
        if (rule is null)
        {
            rule = new MerchantRule { GroupId = groupId, MatchText = matchText, DisplayText = display };
            db.MerchantRules.Add(rule);
        }
        rule.CategoryId = category.Id;

        // File the other uncategorized ones from the same store now.
        var budgetStart = await FinanceSettingsService.GetBudgetStartAsync(db, ct);
        var others = await NeedsCategory(db, userId, budgetStart).Where(t => t.Id != transactionId).ToListAsync(ct);
        var filed = 0;
        foreach (var other in others.Where(o => RuleMatches(matchText, o.Description, o.MerchantName, o.BankDescription)))
        {
            other.CategoryId = category.Id;
            filed++;
        }
        await db.SaveChangesAsync(ct);
        return new CategorizeResult(filed, display);
    }

    public async Task<List<MerchantRuleDto>> GetMerchantRulesAsync(int groupId, CancellationToken ct = default)
    {
        await using var db = await tenantDb.CreateForGroupAsync(groupId, ct);
        return await db.MerchantRules
            .OrderBy(r => r.DisplayText)
            .Select(r => new MerchantRuleDto(r.Id, r.DisplayText, r.CategoryId, r.Category.Name))
            .ToListAsync(ct);
    }

    public async Task DeleteMerchantRuleAsync(int groupId, int ruleId, CancellationToken ct = default)
    {
        await using var db = await tenantDb.CreateForGroupAsync(groupId, ct);
        var rule = await db.MerchantRules.FirstOrDefaultAsync(r => r.Id == ruleId, ct);
        if (rule is null) return;
        db.MerchantRules.Remove(rule);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>The longest matching store rule's category, for a new bank transaction.</summary>
    public static int? FindRuleCategory(IEnumerable<MerchantRule> rules, string? description, string? merchant, string? bankText) =>
        rules.Where(r => RuleMatches(r.MatchText, description, merchant, bankText))
            .OrderByDescending(r => r.MatchText.Length)
            .Select(r => (int?)r.CategoryId)
            .FirstOrDefault();

    private static bool RuleMatches(string matchText, string? description, string? merchant, string? bankText) =>
        BillMatching.Normalize(description).Contains(matchText)
        || BillMatching.Normalize(merchant).Contains(matchText)
        || BillMatching.Normalize(bankText).Contains(matchText);

    // ------------------------------------------------------------------ Suggestions

    public async Task<List<RecurringSuggestionDto>> GetSuggestionsAsync(int groupId, CancellationToken ct = default)
    {
        await using var db = await tenantDb.CreateForGroupAsync(groupId, ct);
        return await RecurringDetector.FindAsync(db, currentUser.UserId, DateTime.Today, ct);
    }

    public async Task DismissSuggestionAsync(int groupId, DismissSuggestionRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Key) || request.Key.Length > 300)
            throw new ArgumentException("Invalid suggestion.");

        await using var db = await tenantDb.CreateForGroupAsync(groupId, ct);
        if (await db.SuggestionDismissals.AnyAsync(d => d.Key == request.Key, ct)) return;
        db.SuggestionDismissals.Add(new SuggestionDismissal { GroupId = groupId, Key = request.Key });
        await db.SaveChangesAsync(ct);
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>
    /// Active checking accounts, private ones included (they count in totals). Bank-fed accounts use the
    /// bank's available balance when it has one, since pending charges are already out of it.
    /// </summary>
    private static async Task<(decimal Balance, DateTime? AsOf, bool HasChecking)> CheckingBalanceAsync(ApplicationDbContext db, CancellationToken ct)
    {
        var accounts = await db.Accounts
            .Where(a => a.IsActive && a.AccountType == "Checking")
            .Select(a => new { a.CurrentBalance, a.AvailableBalance, a.BalanceAsOf })
            .ToListAsync(ct);
        var balance = accounts.Sum(a => a.BalanceAsOf != null ? a.AvailableBalance ?? a.CurrentBalance : a.CurrentBalance);
        var asOf = accounts.Where(a => a.BalanceAsOf != null).Select(a => a.BalanceAsOf).Min();
        return (balance, asOf, accounts.Count > 0);
    }

    /// <summary>Budget spending with no category, visible to this member. Card purchases left out of the budget don't need one.</summary>
    internal static IQueryable<Transaction> NeedsCategory(ApplicationDbContext db, string userId, DateTime? budgetStart) =>
        db.Transactions.VisibleTo(db, userId)
            .Where(t => t.Kind == TransactionKinds.Spending && t.CategoryId == null
                && (budgetStart == null || t.Date >= budgetStart)
                && (t.Account == null || t.Account.CountPurchasesInBudget));
}
