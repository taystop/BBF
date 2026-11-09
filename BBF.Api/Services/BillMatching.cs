using BBF.Data;
using BBF.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace BBF.Api.Services;

/// <summary>
/// Turns recurring rules into expected bills and marks them paid when a matching transaction shows up.
/// Every method works on a context the caller supplies: group-scoped (CurrentGroupId set) from API
/// calls and Plaid syncs, or unscoped from the background job. Matching always compares GroupId
/// explicitly, so an unscoped context can never pair one group's bill with another group's transaction.
/// </summary>
public static class BillMatching
{
    /// <summary>How far ahead expected bills are created.</summary>
    public static readonly TimeSpan Horizon = TimeSpan.FromDays(60);

    // A bill can be paid a little early or a little late and still match.
    private const int DaysEarly = 10;
    private const int DaysLate = 5;

    /// <summary>Variable bills (utilities) still match when the amount is within this fraction of expected.</summary>
    private const decimal AmountTolerance = 0.25m;

    public static DateTime Advance(DateTime date, string frequency) => frequency switch
    {
        "Weekly" => date.AddDays(7),
        "BiWeekly" => date.AddDays(14),
        "Yearly" => date.AddYears(1),
        _ => date.AddMonths(1), // "Monthly"
    };

    /// <summary>
    /// Posts due transactions for AutoPost rules (cash/manual items) and creates expected bills for
    /// every other rule out to <see cref="Horizon"/>. Caller saves.
    /// </summary>
    public static async Task GenerateAsync(ApplicationDbContext db, DateTime today, CancellationToken ct)
    {
        var horizon = today + Horizon;
        var rules = await db.RecurringTransactions
            .Where(r => r.IsActive && r.NextOccurrence <= horizon)
            .ToListAsync(ct);

        foreach (var rule in rules)
        {
            if (rule.AutoPost)
                await PostDueAsync(db, rule, today, ct);
            else
                await GenerateForRuleAsync(db, rule, today, ct);
        }
    }

    /// <summary>Creates expected bills for one non-AutoPost rule, advancing its NextOccurrence. Caller saves.</summary>
    public static async Task GenerateForRuleAsync(ApplicationDbContext db, RecurringTransaction rule, DateTime today, CancellationToken ct)
    {
        var horizon = today + Horizon;
        var existingDates = (await db.BillOccurrences
                .Where(b => b.RecurringTransactionId == rule.Id && b.DueDate >= rule.NextOccurrence)
                .Select(b => b.DueDate)
                .ToListAsync(ct))
            .ToHashSet();

        var iterations = 0;
        while (rule.IsActive && rule.NextOccurrence <= horizon && iterations++ < 500)
        {
            if (rule.EndDate is not null && rule.NextOccurrence > rule.EndDate)
            {
                rule.IsActive = false;
                break;
            }

            // Don't back-fill bills from long ago (e.g. a rule whose start date is far in the past).
            if (rule.NextOccurrence >= today.AddDays(-35) && existingDates.Add(rule.NextOccurrence))
            {
                db.BillOccurrences.Add(new BillOccurrence
                {
                    GroupId = rule.GroupId,
                    RecurringTransactionId = rule.Id,
                    DueDate = rule.NextOccurrence,
                    ExpectedAmount = rule.Amount,
                    Status = BillStatuses.Expected,
                });
            }

            rule.NextOccurrence = Advance(rule.NextOccurrence, rule.Frequency);
        }
    }

    private static async Task PostDueAsync(ApplicationDbContext db, RecurringTransaction rule, DateTime today, CancellationToken ct)
    {
        var iterations = 0;
        while (rule.IsActive && rule.NextOccurrence <= today && iterations++ < 500)
        {
            db.Transactions.Add(new Transaction
            {
                Amount = rule.Amount,
                Date = rule.NextOccurrence,
                Description = rule.Payee,
                CategoryId = rule.CategoryId,
                AccountId = rule.AccountId,
                GroupId = rule.GroupId,
                Source = "Recurring",
                Kind = rule.Amount < 0 ? TransactionKinds.Income : TransactionKinds.Spending,
                RecurringTransactionId = rule.Id,
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync(ct);
            await AccountsService.RecalculateBalanceAsync(rule.AccountId, db);

            rule.NextOccurrence = Advance(rule.NextOccurrence, rule.Frequency);
            if (rule.EndDate is not null && rule.NextOccurrence > rule.EndDate)
                rule.IsActive = false;
        }
    }

    /// <summary>
    /// Pairs expected bills due around today with unmatched transactions: same group, same direction,
    /// date within the early/late window, and either the description contains the rule's match text
    /// (amount within 25%) or the amount is exact on the rule's account. Caller saves.
    /// </summary>
    public static async Task MatchAsync(ApplicationDbContext db, DateTime today, CancellationToken ct)
    {
        var bills = await db.BillOccurrences
            .Include(b => b.RecurringTransaction)
            .Where(b => b.Status == BillStatuses.Expected
                && b.TransactionId == null
                && b.DueDate >= today.AddDays(-35)
                && b.DueDate <= today.AddDays(DaysEarly))
            .OrderBy(b => b.DueDate)
            .ToListAsync(ct);
        if (bills.Count == 0) return;

        var from = bills.Min(b => b.DueDate).AddDays(-DaysEarly);
        var to = bills.Max(b => b.DueDate).AddDays(DaysLate);
        var linkedIds = db.BillOccurrences.Where(b => b.TransactionId != null).Select(b => b.TransactionId);
        var paycheckIds = db.Paychecks.Where(p => p.TransactionId != null).Select(p => p.TransactionId);

        var candidates = await db.Transactions
            .Where(t => t.Date >= from && t.Date <= to
                && t.Kind != TransactionKinds.Transfer
                && !linkedIds.Contains(t.Id)
                && !paycheckIds.Contains(t.Id))
            .ToListAsync(ct);

        var used = new HashSet<int>();
        foreach (var bill in bills)
        {
            var rule = bill.RecurringTransaction;
            var matchText = Normalize(string.IsNullOrWhiteSpace(rule.MatchText) ? rule.Payee : rule.MatchText);

            var match = candidates
                .Where(t => !used.Contains(t.Id)
                    && t.GroupId == bill.GroupId
                    && Math.Sign(t.Amount) == Math.Sign(bill.ExpectedAmount)
                    && t.Date >= bill.DueDate.AddDays(-DaysEarly)
                    && t.Date <= bill.DueDate.AddDays(DaysLate)
                    && (TextMatches(t, matchText) && AmountClose(t.Amount, bill.ExpectedAmount)
                        || t.AccountId == rule.AccountId && Math.Abs(t.Amount - bill.ExpectedAmount) < 0.01m))
                .OrderBy(t => Math.Abs((t.Date - bill.DueDate).TotalDays))
                .FirstOrDefault();
            if (match is null) continue;

            used.Add(match.Id);
            bill.TransactionId = match.Id;
            bill.Status = BillStatuses.Paid;
            bill.PaidAt = DateTime.UtcNow;

            match.RecurringTransactionId ??= rule.Id;
            if (match.CategoryId is null && rule.CategoryId is not null)
                match.CategoryId = rule.CategoryId;
        }
    }

    /// <summary>
    /// Returns any bills paid by these transactions to Expected. Must run before the transactions are
    /// deleted, since the bill-to-transaction foreign key has no database-side cascade. Caller saves.
    /// </summary>
    public static async Task UnlinkTransactionsAsync(ApplicationDbContext db, IReadOnlyCollection<int> transactionIds, CancellationToken ct)
    {
        if (transactionIds.Count == 0) return;
        var bills = await db.BillOccurrences
            .Where(b => b.TransactionId != null && transactionIds.Contains(b.TransactionId.Value))
            .ToListAsync(ct);
        foreach (var bill in bills)
        {
            bill.TransactionId = null;
            bill.Status = BillStatuses.Expected;
            bill.PaidAt = null;
        }
    }

    private static bool AmountClose(decimal actual, decimal expected)
    {
        var tolerance = Math.Max(Math.Abs(expected) * AmountTolerance, 2m);
        return Math.Abs(actual - expected) <= tolerance;
    }

    internal static bool TextMatches(Transaction t, string matchText)
    {
        if (matchText.Length < 3) return false;
        return Normalize(t.Description).Contains(matchText)
            || (t.MerchantName is not null && Normalize(t.MerchantName).Contains(matchText));
    }

    internal static string Normalize(string? text) =>
        new((text ?? "").ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
}
