using BBF.Data;
using BBF.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace BBF.Api.Services;

/// <summary>
/// Turns pay schedules into expected paychecks and matches each to the real deposit. Works on a context
/// the caller supplies (group-scoped, or the background job's unscoped one); matching always compares
/// GroupId explicitly. Mirrors <see cref="BillMatching"/>.
/// </summary>
public static class PaycheckMatching
{
    // Weekend and holiday paydays land a day or two off.
    private const int DaysEarly = 3;
    private const int DaysLate = 3;

    public static readonly string[] Frequencies = ["Weekly", "BiWeekly", "Monthly"];

    /// <summary>
    /// First payday on or after <paramref name="date"/>, counting forward or back from the schedule's
    /// anchor payday (so entering next Friday's payday still fills in the recent ones).
    /// </summary>
    public static DateTime FirstPaydayOnOrAfter(DateTime anchor, string frequency, DateTime date)
    {
        date = date.Date;
        var payday = anchor.Date;
        var iterations = 0;
        while (payday < date && iterations++ < 5000)
            payday = BillMatching.Advance(payday, frequency);
        while (iterations++ < 5000)
        {
            var previous = Retreat(payday, frequency);
            if (previous < date) break;
            payday = previous;
        }
        return payday;
    }

    private static DateTime Retreat(DateTime date, string frequency) => frequency switch
    {
        "Weekly" => date.AddDays(-7),
        "BiWeekly" => date.AddDays(-14),
        _ => date.AddMonths(-1), // "Monthly"
    };

    /// <summary>Creates expected paychecks for every active schedule out to the bill horizon. Caller saves.</summary>
    public static async Task GenerateAsync(ApplicationDbContext db, DateTime today, CancellationToken ct)
    {
        var horizon = today + BillMatching.Horizon;
        var schedules = await db.PaySchedules
            .Where(s => s.IsActive && s.NextPayday <= horizon)
            .ToListAsync(ct);
        foreach (var schedule in schedules)
            await GenerateForScheduleAsync(db, schedule, today, ct);
    }

    public static async Task GenerateForScheduleAsync(ApplicationDbContext db, PaySchedule schedule, DateTime today, CancellationToken ct)
    {
        var horizon = today + BillMatching.Horizon;
        var existingDates = (await db.Paychecks
                .Where(p => p.PayScheduleId == schedule.Id && p.PayDate >= schedule.NextPayday)
                .Select(p => p.PayDate)
                .ToListAsync(ct))
            .ToHashSet();

        var iterations = 0;
        while (schedule.IsActive && schedule.NextPayday <= horizon && iterations++ < 500)
        {
            if (schedule.NextPayday >= today.AddDays(-35) && existingDates.Add(schedule.NextPayday))
            {
                db.Paychecks.Add(new Paycheck
                {
                    GroupId = schedule.GroupId,
                    PayScheduleId = schedule.Id,
                    PayDate = schedule.NextPayday,
                    ExpectedAmount = schedule.ExpectedAmount,
                    Status = PaycheckStatuses.Expected,
                });
            }
            schedule.NextPayday = BillMatching.Advance(schedule.NextPayday, schedule.Frequency);
        }
    }

    /// <summary>
    /// Matches expected paychecks around today to incoming deposits: same group, within a few days of
    /// payday, in the deposit account when one is set, and either containing the match text or close to
    /// the normal amount (overtime can make it much larger). Caller saves.
    /// </summary>
    public static async Task MatchAsync(ApplicationDbContext db, DateTime today, CancellationToken ct)
    {
        var paychecks = await db.Paychecks
            .Include(p => p.PaySchedule)
            .Where(p => p.Status == PaycheckStatuses.Expected
                && p.TransactionId == null
                && p.PayDate >= today.AddDays(-35)
                && p.PayDate <= today.AddDays(DaysEarly))
            .OrderBy(p => p.PayDate)
            .ToListAsync(ct);
        if (paychecks.Count == 0) return;

        var from = paychecks.Min(p => p.PayDate).AddDays(-DaysEarly);
        var to = paychecks.Max(p => p.PayDate).AddDays(DaysLate);
        var linkedIds = db.Paychecks.Where(p => p.TransactionId != null).Select(p => p.TransactionId);

        var candidates = await db.Transactions
            .Where(t => t.Date >= from && t.Date <= to
                && t.Amount < 0 // money in
                && t.Kind != TransactionKinds.Transfer
                && !linkedIds.Contains(t.Id))
            .ToListAsync(ct);

        var used = new HashSet<int>();
        foreach (var paycheck in paychecks)
        {
            var schedule = paycheck.PaySchedule;
            var matchText = BillMatching.Normalize(schedule.MatchText);
            var expected = paycheck.ExpectedAmount;

            var match = candidates
                .Where(t => !used.Contains(t.Id)
                    && t.GroupId == paycheck.GroupId
                    && t.Date >= paycheck.PayDate.AddDays(-DaysEarly)
                    && t.Date <= paycheck.PayDate.AddDays(DaysLate)
                    && (schedule.DepositAccountId is null || t.AccountId == schedule.DepositAccountId)
                    && (matchText.Length >= 3
                        ? BillMatching.TextMatches(t, matchText) && -t.Amount >= expected * 0.25m
                        : -t.Amount >= expected * 0.75m && -t.Amount <= expected * 3m))
                .OrderBy(t => Math.Abs((t.Date - paycheck.PayDate).TotalDays))
                .ThenBy(t => Math.Abs(-t.Amount - expected))
                .FirstOrDefault();
            if (match is null) continue;

            used.Add(match.Id);
            paycheck.TransactionId = match.Id;
            paycheck.ActualAmount = -match.Amount;
            paycheck.Status = PaycheckStatuses.Received;
            paycheck.ReceivedAt = DateTime.UtcNow;

            if (!match.KindSetManually)
            {
                match.Kind = TransactionKinds.Income;
                match.CategoryId = null;
            }
        }
    }

    /// <summary>Returns paychecks matched to these transactions to Expected. Run before deleting them. Caller saves.</summary>
    public static async Task UnlinkTransactionsAsync(ApplicationDbContext db, IReadOnlyCollection<int> transactionIds, CancellationToken ct)
    {
        if (transactionIds.Count == 0) return;
        var paychecks = await db.Paychecks
            .Where(p => p.TransactionId != null && transactionIds.Contains(p.TransactionId.Value))
            .ToListAsync(ct);
        foreach (var paycheck in paychecks)
        {
            paycheck.TransactionId = null;
            paycheck.ActualAmount = null;
            paycheck.ReceivedAt = null;
            paycheck.Status = PaycheckStatuses.Expected;
        }
    }
}
