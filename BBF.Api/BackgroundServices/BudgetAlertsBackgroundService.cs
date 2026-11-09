using BBF.Api.Notifications;
using BBF.Api.Services;
using BBF.Data;
using BBF.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace BBF.Api.BackgroundServices;

/// <summary>
/// Every 30 minutes, for each group with budget data: records how each finished pay period ended (the
/// leftover-to-savings question), then, between 8am and 9pm only, creates budget alerts and pushes them:
/// category near or over its limit, bill due tomorrow, low balance ahead, payday summary, leftover
/// question, and an evening reminder to categorize. Each alert happens once (see <see cref="AlertKeys"/>),
/// appears in the member's alert list in the app, and is pushed unless they turned push off. Alerts about
/// a private bill go only to its owner; the rest are group totals.
/// </summary>
public class BudgetAlertsBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<BudgetAlertsBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(30);
    private const int QuietUntilHour = 8;
    private const int QuietFromHour = 21;
    private const int PaydaySummaryHour = 9;
    private const int CategorizeReminderHour = 19;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Let the app finish starting (and migrations settle) before the first pass.
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Budget alerts pass failed");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
        var notifications = scope.ServiceProvider.GetRequiredService<INotificationSender>();

        List<int> groupIds;
        await using (var db = await dbFactory.CreateDbContextAsync(ct))
        {
            groupIds = await db.UserGroups
                .Where(g => db.Accounts.Any(a => a.GroupId == g.Id) || db.PaySchedules.Any(s => s.GroupId == g.Id))
                .Select(g => g.Id)
                .ToListAsync(ct);
        }

        foreach (var groupId in groupIds)
        {
            try
            {
                // Trusted cross-tenant job: one context per group, scoped to it like an API request would be.
                await using var db = await dbFactory.CreateDbContextAsync(ct);
                db.CurrentGroupId = groupId;
                await RunForGroupAsync(db, groupId, notifications, ct);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                logger.LogError(ex, "Budget alerts failed for group {GroupId}", groupId);
            }
        }
    }

    private static async Task RunForGroupAsync(ApplicationDbContext db, int groupId, INotificationSender notifications, CancellationToken ct)
    {
        var now = DateTime.Now;
        var today = now.Date;
        var current = await PaychecksService.BuildPeriodAsync(db, groupId, "", today, ct);
        await RecordFinishedPeriodAsync(db, groupId, current, today, ct);

        if (now.Hour < QuietUntilHour || now.Hour >= QuietFromHour) return;

        var members = await db.UserGroupMembers.Where(m => m.GroupId == groupId).Select(m => m.UserId).Distinct().ToListAsync(ct);
        if (members.Count == 0) return;
        var prefs = await db.AlertPreferences.ToDictionaryAsync(p => p.UserId, ct);
        var sender = new AlertWriter(db, groupId, prefs, notifications);

        // Leftover from the pay period that just ended.
        var prompt = await AlertsService.GetLeftoverPromptAsync(db, current.Start, ct);
        if (prompt is not null)
        {
            await sender.SendAsync(members, FinanceAlertKinds.Leftover, AlertKeys.Leftover(prompt.PeriodStart),
                $"{prompt.Amount:C} left from last pay period",
                "Move it to savings? If you don't, it carries over to this pay period.", "/money", p => p.Payday, ct);
        }

        // Payday summary, once the morning's deposits have had a chance to land.
        if (current.Start == today && current.Paychecks.Count > 0 && now.Hour >= PaydaySummaryHour)
        {
            await sender.SendAsync(members, FinanceAlertKinds.Payday, AlertKeys.Payday(today),
                "Payday",
                $"This paycheck covers {current.BillsTotal:C} in bills, leaving about {current.Leftover:C} to spend through {current.End:MMM d}.",
                "/paychecks", p => p.Payday, ct);
        }

        // Bills due tomorrow. A bill from a private account goes only to its owner.
        var tomorrow = today.AddDays(1);
        var dueTomorrow = await db.BillOccurrences
            .Include(b => b.RecurringTransaction).ThenInclude(r => r.Account)
            .Where(b => b.Status == BillStatuses.Expected && b.DueDate == tomorrow && b.ExpectedAmount > 0)
            .ToListAsync(ct);
        foreach (var bill in dueTomorrow)
        {
            var owner = bill.RecurringTransaction.Account.PrivateToUserId;
            await sender.SendAsync(owner is not null ? [owner] : members, FinanceAlertKinds.BillDue, AlertKeys.Bill(bill.Id),
                "Bill due tomorrow",
                $"{bill.RecurringTransaction.Payee}: {bill.ExpectedAmount:C}", "/recurring", p => p.BillsAndBalance, ct);
        }

        // Low balance in the coming week.
        var forecast = await MoneyService.BuildForecastAsync(db, "", 8, ct);
        var tight = forecast.Days.FirstOrDefault(d => d.IsTight);
        if (tight is not null && await db.Accounts.AnyAsync(a => a.IsActive && a.AccountType == "Checking", ct))
        {
            var when = tight.Date == today ? "today" : $"on {tight.Date:ddd, MMM d}";
            await sender.SendAsync(members, FinanceAlertKinds.LowBalance, AlertKeys.LowBalance(tight.Date),
                "Low balance ahead",
                $"Checking could drop to {tight.EndBalance:C} {when}, below your {forecast.LowBalanceThreshold:C} warning.",
                "/money", p => p.BillsAndBalance, ct);
        }

        // Categories at 80% and 100% of this month's budget.
        var month = await db.BudgetPeriods
            .Include(p => p.LineItems).ThenInclude(li => li.Category)
            .FirstOrDefaultAsync(p => p.StartDate <= today && today < p.EndDate, ct);
        if (month is not null)
        {
            var spending = await BudgetPeriodsService.GetSpendingAsync(db, month.StartDate, month.EndDate, ct);
            foreach (var line in month.LineItems)
            {
                var budget = line.BudgetedAmount + line.RolledOverAmount;
                if (budget <= 0) continue;
                var spent = spending.GetValueOrDefault(line.CategoryId);
                var name = line.Category.Name;

                if (spent >= budget)
                {
                    await sender.SendAsync(members, FinanceAlertKinds.CategoryLimit, AlertKeys.Category(line.CategoryId, month.StartDate, 100),
                        $"{name} is over budget",
                        $"{spent:C} spent of {budget:C} this month.", "/budget", p => p.CategoryLimits, ct);
                }
                else if (spent >= budget * 0.8m
                    && !await db.FinanceAlerts.AnyAsync(a => a.Key == AlertKeys.Category(line.CategoryId, month.StartDate, 100), ct))
                {
                    await sender.SendAsync(members, FinanceAlertKinds.CategoryLimit, AlertKeys.Category(line.CategoryId, month.StartDate, 80),
                        $"{name} is at {spent / budget:P0}",
                        $"{spent:C} of {budget:C} spent, {budget - spent:C} left this month.", "/budget", p => p.CategoryLimits, ct);
                }
            }
        }

        // Evening reminder: each member's own count, since private transactions only count for their owner.
        if (now.Hour >= CategorizeReminderHour)
        {
            var budgetStart = await FinanceSettingsService.GetBudgetStartAsync(db, ct);
            foreach (var member in members)
            {
                var count = await MoneyService.NeedsCategory(db, member, budgetStart).CountAsync(ct);
                if (count == 0) continue;
                await sender.SendAsync([member], FinanceAlertKinds.Categorize, AlertKeys.Categorize(today),
                    $"{count} transaction{(count == 1 ? "" : "s")} to categorize",
                    "A minute now keeps your budget accurate.", "/review", p => p.CategorizeReminder, ct);
            }
        }
    }

    /// <summary>
    /// When a new pay period has started, records how the one before it ended. A positive leftover is
    /// a question for the household (Pending until answered, carrying over meanwhile); zero or negative
    /// just carries over. Only looks back three weeks, so turning this on doesn't rewrite old periods.
    /// The leftover is never more than checking really had spare when the period ended. A question
    /// still unanswered is worked out again each run, so late-posting transactions correct it.
    /// </summary>
    private static async Task RecordFinishedPeriodAsync(ApplicationDbContext db, int groupId, BBF.Shared.Contracts.Paychecks.PayPeriodDto current, DateTime today, CancellationToken ct)
    {
        if (!current.HasPaySchedules || current.Paychecks.Count == 0) return;

        var previous = await PaychecksService.BuildPeriodAsync(db, groupId, "", current.Start.AddDays(-1), ct);
        if (previous.Paychecks.Count == 0 || previous.End != current.Start.AddDays(-1)) return;
        if (previous.Start < today.AddDays(-21)) return;
        var existing = await db.LeftoverDecisions.FirstOrDefaultAsync(d => d.PeriodStart == previous.Start, ct);
        if (existing is not null && existing.Decision != LeftoverDecisions.Pending) return;

        var amount = Math.Round(Math.Min(previous.Leftover, await CashSpareAsync(db, previous, ct) ?? previous.Leftover), 2);
        var decision = amount > 0 ? LeftoverDecisions.Pending : LeftoverDecisions.CarriedOver;
        if (existing is null)
        {
            db.LeftoverDecisions.Add(new LeftoverDecision
            {
                GroupId = groupId,
                PeriodStart = previous.Start,
                PeriodEnd = previous.End,
                NextPeriodStart = current.Start,
                Amount = amount,
                Decision = decision,
            });
        }
        else if (existing.Amount != amount)
        {
            existing.Amount = amount;
            existing.Decision = decision;
        }
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// What checking had spare when the period ended: its balance at the end of the last day, less the
    /// period's bills that hadn't come out yet. Null when no checking account is linked.
    /// </summary>
    private static async Task<decimal?> CashSpareAsync(ApplicationDbContext db, BBF.Shared.Contracts.Paychecks.PayPeriodDto period, CancellationToken ct)
    {
        var balance = await MoneyService.CheckingBalanceAtEndOfAsync(db, period.End, ct);
        if (balance is null) return null;

        var billIds = period.Bills.Select(b => b.BillId).ToList();
        var end = period.End.Date;
        var stillOwed = await db.BillOccurrences
            .Where(b => billIds.Contains(b.Id)
                && (b.Status == BillStatuses.Expected
                    || b.Status == BillStatuses.Paid && b.Transaction != null && b.Transaction.Date > end))
            .SumAsync(b => (decimal?)(b.Status == BillStatuses.Paid ? b.Transaction!.Amount : b.ExpectedAmount), ct) ?? 0m;

        return balance.Value - stillOwed;
    }

    /// <summary>Creates each member's alert once, honoring their settings, and pushes it.</summary>
    private sealed class AlertWriter(
        ApplicationDbContext db, int groupId, Dictionary<string, AlertPreferences> prefs, INotificationSender notifications)
    {
        public async Task SendAsync(IEnumerable<string> userIds, string kind, string key, string title, string body, string link,
            Func<AlertPreferences, bool> wants, CancellationToken ct)
        {
            foreach (var userId in userIds.Distinct())
            {
                var p = prefs.GetValueOrDefault(userId) ?? new AlertPreferences();
                if (!wants(p)) continue;
                if (await db.FinanceAlerts.AnyAsync(a => a.UserId == userId && a.Key == key, ct)) continue;

                db.FinanceAlerts.Add(new FinanceAlert
                {
                    GroupId = groupId, UserId = userId, Kind = kind, Key = key, Title = title, Body = body, Link = link,
                });
                await db.SaveChangesAsync(ct);

                if (p.Push)
                {
                    await notifications.SendToUserAsync(userId, new NotificationMessage(title, body,
                        new Dictionary<string, string> { ["link"] = link, ["groupId"] = groupId.ToString() }), ct);
                }
            }
        }
    }
}
