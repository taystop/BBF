using BBF.Api.Auth;
using BBF.Data;
using BBF.Data.Entities;
using BBF.Shared.Contracts.Planning;
using Microsoft.EntityFrameworkCore;

namespace BBF.Api.Services;

/// <summary>
/// Monthly planning: what's expected to come in each month against the category budget and goal
/// set-asides ("left to assign"), months with an extra paycheck ahead of time, and a look back at
/// how a month went. Paydays further out than tracked paychecks are projected from the schedules.
/// </summary>
public class PlanningService(ITenantDbContextFactory tenantDb, ICurrentUserContext currentUser)
{
    private const int ExtraPaycheckLookaheadMonths = 12;
    private const int TrendMonths = 6;

    // ------------------------------------------------------------------ Month plan

    public async Task<MonthPlanDto> GetMonthPlanAsync(int groupId, DateTime month, CancellationToken ct = default)
    {
        month = new DateTime(month.Year, month.Month, 1);
        var next = month.AddMonths(1);

        await using var db = await tenantDb.CreateForGroupAsync(groupId, ct);
        var userId = currentUser.UserId;
        var names = await PaychecksService.MemberNamesAsync(db, groupId, ct);

        var schedules = await db.PaySchedules.ToListAsync(ct);
        var rows = await db.Paychecks
            .Where(p => p.PayDate >= month && p.PayDate < next)
            .ToListAsync(ct);

        var paydays = new List<PlannedPaydayDto>();
        decimal expected = 0, received = 0;
        var paydayDates = new HashSet<DateTime>();

        foreach (var s in schedules)
        {
            var hidden = Privacy.IsHiddenFrom(s, userId);
            var tracked = rows.Where(r => r.PayScheduleId == s.Id).ToDictionary(r => r.PayDate);
            var dates = (s.IsActive ? Project(s, month, next) : Enumerable.Empty<DateTime>())
                .Concat(tracked.Keys)
                .Distinct()
                .OrderBy(d => d)
                .ToList();

            for (var i = 0; i < dates.Count; i++)
            {
                var date = dates[i];
                tracked.TryGetValue(date, out var row);
                var status = row?.Status ?? "Projected";
                var amount = row is null ? s.ExpectedAmount
                    : row.Status == PaycheckStatuses.Received ? row.ActualAmount ?? row.ExpectedAmount
                    : row.ExpectedAmount;

                if (status != PaycheckStatuses.Skipped)
                {
                    expected += amount;
                    paydayDates.Add(date);
                    if (status == PaycheckStatuses.Received) received += amount;
                }

                paydays.Add(new PlannedPaydayDto(
                    date,
                    hidden ? Privacy.HiddenPaycheckName : s.Name,
                    names.GetValueOrDefault(s.UserId, "Former member"),
                    hidden ? null : amount,
                    status, hidden,
                    IsExtra: IsExtraPayday(s.Frequency, i)));
            }
        }

        var budgeted = await BudgetedForMonthAsync(db, month, ct);
        var goals = paydayDates.Count * await PaychecksService.GoalsPerPaydayAsync(db, ct);

        var today = DateTime.Today;
        var lookaheadFrom = month > today ? month : new DateTime(today.Year, today.Month, 1);
        var extras = ExtraPaycheckMonths(schedules, userId, lookaheadFrom, lookaheadFrom.AddMonths(ExtraPaycheckLookaheadMonths));

        return new MonthPlanDto(
            month, schedules.Any(s => s.IsActive),
            expected, received, budgeted, goals,
            expected - budgeted - goals,
            paydays.OrderBy(p => p.Date).ThenBy(p => p.Name).ToList(),
            extras);
    }

    /// <summary>The month's category budget: its budget period if one exists, otherwise what one would start with.</summary>
    private static async Task<decimal> BudgetedForMonthAsync(ApplicationDbContext db, DateTime month, CancellationToken ct)
    {
        var period = await db.BudgetPeriods.Include(p => p.LineItems).FirstOrDefaultAsync(p => p.StartDate == month, ct);
        if (period is not null)
            return period.LineItems.Sum(li => li.BudgetedAmount + li.RolledOverAmount);
        return await db.BudgetCategories.Where(c => c.IsActive).SumAsync(c => (decimal?)c.MonthlyLimit, ct) ?? 0m;
    }

    /// <summary>A biweekly schedule's third payday in a month, or a weekly one's fifth, is the extra one.</summary>
    private static bool IsExtraPayday(string frequency, int indexInMonth) =>
        frequency == "BiWeekly" && indexInMonth >= 2 || frequency == "Weekly" && indexInMonth >= 4;

    private static IEnumerable<DateTime> Project(PaySchedule s, DateTime from, DateTime toExclusive)
    {
        var date = PaycheckMatching.FirstPaydayOnOrAfter(s.FirstPayday, s.Frequency, from);
        var guard = 0;
        while (date < toExclusive && guard++ < 500)
        {
            yield return date;
            date = BillMatching.Advance(date, s.Frequency);
        }
    }

    private static List<ExtraPaycheckMonthDto> ExtraPaycheckMonths(List<PaySchedule> schedules, string userId, DateTime from, DateTime to)
    {
        var extras = new Dictionary<DateTime, (List<string> Names, decimal? Amount)>();
        foreach (var s in schedules.Where(s => s.IsActive && s.Frequency is "BiWeekly" or "Weekly"))
        {
            var hidden = Privacy.IsHiddenFrom(s, userId);
            foreach (var monthGroup in Project(s, from, to).GroupBy(d => new DateTime(d.Year, d.Month, 1)))
            {
                var extraCount = monthGroup.Count() - (s.Frequency == "BiWeekly" ? 2 : 4);
                if (extraCount <= 0) continue;

                if (!extras.TryGetValue(monthGroup.Key, out var entry)) entry = ([], 0m);
                entry.Names.Add(hidden ? Privacy.HiddenPaycheckName : s.Name);
                entry.Amount = hidden || entry.Amount is null ? null : entry.Amount + s.ExpectedAmount * extraCount;
                extras[monthGroup.Key] = entry;
            }
        }
        return extras.OrderBy(e => e.Key).Select(e => new ExtraPaycheckMonthDto(e.Key, e.Value.Names, e.Value.Amount)).ToList();
    }

    // ------------------------------------------------------------------ Month in review

    public async Task<MonthReviewDto> GetMonthReviewAsync(int groupId, DateTime month, CancellationToken ct = default)
    {
        month = new DateTime(month.Year, month.Month, 1);
        var next = month.AddMonths(1);

        await using var db = await tenantDb.CreateForGroupAsync(groupId, ct);
        var userId = currentUser.UserId;
        var budgetStart = await FinanceSettingsService.GetBudgetStartAsync(db, ct);
        bool InBudget(DateTime m) => budgetStart is not DateTime bs || m.AddMonths(1) > bs;

        var spending = await BudgetPeriodsService.GetSpendingAsync(db, month, next, ct);
        var previous = new List<Dictionary<int, decimal>>();
        for (var i = 1; i <= 3; i++)
        {
            var m = month.AddMonths(-i);
            if (!InBudget(m)) break;
            previous.Add(await BudgetPeriodsService.GetSpendingAsync(db, m, m.AddMonths(1), ct));
        }

        // Categories: the month's budget lines, plus anything else that had spending.
        var period = await db.BudgetPeriods.Include(p => p.LineItems).FirstOrDefaultAsync(p => p.StartDate == month, ct);
        var categories = await db.BudgetCategories.ToListAsync(ct);
        var budgetedById = period is not null
            ? period.LineItems.GroupBy(li => li.CategoryId).ToDictionary(g => g.Key, g => g.Sum(li => li.BudgetedAmount + li.RolledOverAmount))
            : categories.Where(c => c.IsActive).ToDictionary(c => c.Id, c => c.MonthlyLimit);

        var categoryRows = categories
            .Where(c => budgetedById.ContainsKey(c.Id) || spending.ContainsKey(c.Id))
            .Select(c => new CategoryReviewDto(
                c.Id, c.Name, c.Color,
                budgetedById.GetValueOrDefault(c.Id),
                spending.GetValueOrDefault(c.Id),
                previous.Count > 0 ? previous[0].GetValueOrDefault(c.Id) : null,
                previous.Count > 0 ? Math.Round(previous.Average(p => p.GetValueOrDefault(c.Id)), 2) : null))
            .Where(r => r.Budgeted != 0 || r.Spent != 0 || (r.Average ?? 0) != 0)
            .OrderByDescending(r => r.Spent)
            .ToList();

        // Six-month trend of income and budget spending, in one query.
        var trendStart = month.AddMonths(-(TrendMonths - 1));
        var from = budgetStart is DateTime cutoff && cutoff > trendStart ? cutoff : trendStart;
        var trendRows = await db.Transactions
            .Where(t => t.Date >= from && t.Date < next
                && (t.Kind == TransactionKinds.Income
                    || t.Kind == TransactionKinds.Spending && (t.Account == null || t.Account.CountPurchasesInBudget)))
            .GroupBy(t => new { t.Date.Year, t.Date.Month, t.Kind })
            .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Kind, Total = g.Sum(t => t.Amount) })
            .ToListAsync(ct);
        var trend = Enumerable.Range(0, TrendMonths)
            .Select(i => trendStart.AddMonths(i))
            .Select(m => new MonthTrendDto(m,
                -trendRows.Where(r => r.Year == m.Year && r.Month == m.Month && r.Kind == TransactionKinds.Income).Sum(r => r.Total),
                trendRows.Where(r => r.Year == m.Year && r.Month == m.Month && r.Kind == TransactionKinds.Spending).Sum(r => r.Total)))
            .ToList();
        var thisMonth = trend[^1];

        // Money that went into savings accounts (transfers in, minus transfers out).
        var savingsFrom = budgetStart is DateTime sc && sc > month ? sc : month;
        var movedToSavings = -(await db.Transactions
            .Where(t => t.Date >= savingsFrom && t.Date < next && t.Kind == TransactionKinds.Transfer
                && t.Account != null && t.Account.AccountType == "Savings")
            .SumAsync(t => (decimal?)t.Amount, ct) ?? 0m);

        // Where the money went: merchant names are details, so only what this member can see.
        var top = await db.Transactions.VisibleTo(db, userId)
            .Where(t => t.Date >= savingsFrom && t.Date < next && t.Kind == TransactionKinds.Spending
                && (t.Account == null || t.Account.CountPurchasesInBudget))
            .GroupBy(t => t.MerchantName ?? t.Description)
            .Select(g => new { Name = g.Key, Spent = g.Sum(t => t.Amount), Count = g.Count() })
            .OrderByDescending(g => g.Spent)
            .Take(5)
            .ToListAsync(ct);

        return new MonthReviewDto(
            month,
            BeforeBudgetStart: !InBudget(month),
            thisMonth.Income, thisMonth.Spending, thisMonth.Income - thisMonth.Spending,
            movedToSavings,
            budgetedById.Values.Sum(),
            spending.GetValueOrDefault(0),
            categoryRows, trend,
            top.Where(t => t.Spent > 0).Select(t => new TopMerchantDto(t.Name, t.Spent, t.Count)).ToList());
    }
}
