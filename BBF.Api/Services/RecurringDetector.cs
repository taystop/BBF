using BBF.Data;
using BBF.Data.Entities;
using BBF.Shared.Contracts.Money;
using Microsoft.EntityFrameworkCore;

namespace BBF.Api.Services;

/// <summary>
/// Finds charges and deposits that repeat on a steady schedule in the last four months of transactions:
/// the same payee at least three times in a row, weekly, every two weeks or monthly, at a similar
/// amount. Skips anything already covered by a bill rule or pay schedule, and anything dismissed.
/// Uses only data already synced, so it costs nothing extra from Plaid.
/// </summary>
public static class RecurringDetector
{
    private const int LookbackDays = 120;
    private const int MinOccurrences = 3;
    private const decimal AmountTolerance = 0.30m;
    private const decimal MinIncome = 100m;

    private sealed record Pattern(string Frequency, int MinGap, int MaxGap, int TypicalGap);

    // Checked in this order; the first that fits wins.
    private static readonly Pattern[] Patterns =
    [
        new("Monthly", 26, 35, 30),
        new("BiWeekly", 12, 16, 14),
        new("Weekly", 6, 8, 7),
    ];

    public static async Task<List<RecurringSuggestionDto>> FindAsync(ApplicationDbContext db, string userId, DateTime today, CancellationToken ct)
    {
        var since = today.AddDays(-LookbackDays);
        var billLinked = db.BillOccurrences.Where(b => b.TransactionId != null).Select(b => b.TransactionId);
        var paycheckLinked = db.Paychecks.Where(p => p.TransactionId != null).Select(p => p.TransactionId);

        var rows = await db.Transactions.VisibleTo(db, userId)
            .Where(t => t.Date >= since && t.Kind != TransactionKinds.Transfer && !t.IsPending
                && (t.Account == null || t.Account.CountPurchasesInBudget))
            .Select(t => new
            {
                t.Id, t.Date, t.Amount, t.Description, t.MerchantName, t.CategoryId,
                CategoryName = t.Category != null ? t.Category.Name : null,
                t.AccountId,
                AccountName = t.Account != null ? t.Account.Name : null,
                Covered = t.RecurringTransactionId != null || billLinked.Contains(t.Id) || paycheckLinked.Contains(t.Id),
            })
            .ToListAsync(ct);

        // What's already set up, as normalized text to compare against.
        var ruleTexts = (await db.RecurringTransactions.Where(r => r.IsActive).Select(r => r.MatchText ?? r.Payee).ToListAsync(ct))
            .Concat(await db.PaySchedules.Where(s => s.IsActive).Select(s => s.MatchText ?? s.Name).ToListAsync(ct))
            .Select(BillMatching.Normalize)
            .Where(t => t.Length >= 3)
            .ToList();
        var dismissed = (await db.SuggestionDismissals.Select(d => d.Key).ToListAsync(ct)).ToHashSet();

        var suggestions = new List<RecurringSuggestionDto>();
        var groups = rows
            .Where(r => r.Amount != 0)
            .GroupBy(r => (Name: BillMatching.Normalize(r.MerchantName ?? r.Description), Income: r.Amount < 0))
            .Where(g => g.Key.Name.Length >= 3 && g.Count() >= MinOccurrences);

        foreach (var group in groups)
        {
            if (group.Any(r => r.Covered)) continue;
            if (ruleTexts.Any(t => group.Key.Name.Contains(t) || t.Contains(group.Key.Name))) continue;

            // One entry per day (a split charge counts once), oldest first.
            var series = group
                .GroupBy(r => r.Date.Date)
                .Select(d => d.OrderByDescending(r => Math.Abs(r.Amount)).First())
                .OrderBy(r => r.Date)
                .ToList();
            if (series.Count < MinOccurrences) continue;

            foreach (var pattern in Patterns)
            {
                // The longest run, ending at the latest charge, whose gaps all fit the pattern.
                var run = new List<int> { series.Count - 1 };
                for (var i = series.Count - 2; i >= 0; i--)
                {
                    var gap = (series[i + 1].Date - series[i].Date).TotalDays;
                    if (gap < pattern.MinGap || gap > pattern.MaxGap) break;
                    run.Insert(0, i);
                }
                if (run.Count < MinOccurrences) continue;

                // Most of this payee's activity has to be the pattern (weekly grocery runs aren't a bill).
                if (run.Count < series.Count * 0.6) continue;

                var picked = run.Select(i => series[i]).ToList();
                var amounts = picked.Select(r => Math.Abs(r.Amount)).OrderBy(a => a).ToList();
                var median = amounts[amounts.Count / 2];
                if (amounts.Any(a => Math.Abs(a - median) > Math.Max(median * AmountTolerance, 2m))) continue;

                // Still active: the last one is no more than about one and a half cycles ago.
                var last = picked[^1];
                if ((today - last.Date).TotalDays > pattern.TypicalGap * 1.5) continue;

                if (group.Key.Income && median < MinIncome) continue; // interest, small refunds

                var key = $"{(group.Key.Income ? "in" : "out")}:{group.Key.Name}:{pattern.Frequency}";
                if (dismissed.Contains(key)) break;

                var next = BillMatching.Advance(last.Date, pattern.Frequency);
                while (next < today) next = BillMatching.Advance(next, pattern.Frequency);

                suggestions.Add(new RecurringSuggestionDto(
                    key,
                    last.MerchantName ?? last.Description,
                    last.MerchantName ?? last.Description,
                    Math.Abs(last.Amount),
                    pattern.Frequency,
                    next,
                    last.CategoryId, last.CategoryName,
                    last.AccountId, last.AccountName,
                    picked.Select(r => r.Date).ToList(),
                    group.Key.Income));
                break;
            }
        }

        return suggestions.OrderBy(s => s.IsIncome).ThenBy(s => s.NextDue).ToList();
    }
}
