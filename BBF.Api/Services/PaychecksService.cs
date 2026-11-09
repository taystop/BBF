using BBF.Api.Auth;
using BBF.Data;
using BBF.Data.Entities;
using BBF.Shared.Contracts.Paychecks;
using Microsoft.EntityFrameworkCore;

namespace BBF.Api.Services;

/// <summary>
/// Pay schedules, paychecks and pay periods. A pay period runs from one payday (any earner) up to the
/// day before the next one, so two alternating biweekly earners give weekly periods. Each bill belongs
/// to the period of the last payday before its due date, unless it was moved by hand.
/// </summary>
public class PaychecksService(ITenantDbContextFactory tenantDb, ICurrentUserContext currentUser)
{
    public async Task<List<PayScheduleDto>> GetSchedulesAsync(int groupId, CancellationToken ct = default)
    {
        await using var db = await tenantDb.CreateForGroupAsync(groupId, ct);
        var userId = currentUser.UserId;
        var names = await MemberNamesAsync(db, groupId, ct);
        var today = DateTime.Today;

        var schedules = await db.PaySchedules
            .Include(s => s.DepositAccount)
            .OrderBy(s => s.Name)
            .ToListAsync(ct);
        var nextPaydays = await db.Paychecks
            .Where(p => p.PayDate >= today && p.Status != PaycheckStatuses.Skipped)
            .GroupBy(p => p.PayScheduleId)
            .Select(g => new { ScheduleId = g.Key, Next = g.Min(p => p.PayDate) })
            .ToDictionaryAsync(x => x.ScheduleId, x => x.Next, ct);

        return schedules
            .Select(s => ToDto(s, userId, names, nextPaydays.TryGetValue(s.Id, out var next) ? next : null))
            .ToList();
    }

    public async Task<PayScheduleDto> CreateScheduleAsync(int groupId, SavePayScheduleRequest request, CancellationToken ct = default)
    {
        await using var db = await tenantDb.CreateForGroupAsync(groupId, ct);
        var userId = currentUser.UserId;
        var names = await MemberNamesAsync(db, groupId, ct);
        await ValidateAsync(db, request, names, userId, ct);

        var today = DateTime.Today;
        var schedule = new PaySchedule { GroupId = groupId };
        Apply(schedule, request);
        schedule.NextPayday = PaycheckMatching.FirstPaydayOnOrAfter(schedule.FirstPayday, schedule.Frequency, today.AddDays(-35));
        db.PaySchedules.Add(schedule);
        await db.SaveChangesAsync(ct);

        await RefreshPaychecksAsync(db, schedule, today, ct);
        await db.Entry(schedule).Reference(s => s.DepositAccount).LoadAsync(ct);
        return ToDto(schedule, userId, names, null);
    }

    public async Task UpdateScheduleAsync(int groupId, int scheduleId, SavePayScheduleRequest request, CancellationToken ct = default)
    {
        await using var db = await tenantDb.CreateForGroupAsync(groupId, ct);
        var userId = currentUser.UserId;
        var schedule = await db.PaySchedules.FirstOrDefaultAsync(s => s.Id == scheduleId, ct);
        if (schedule is null) return;
        if (Privacy.IsHiddenFrom(schedule, userId))
            throw new ForbiddenAccessException("Only the earner can change a private paycheck.");

        var names = await MemberNamesAsync(db, groupId, ct);
        await ValidateAsync(db, request, names, userId, ct);
        Apply(schedule, request);

        // The schedule may have changed: rebuild every paycheck that hasn't been matched to a deposit.
        var today = DateTime.Today;
        var unmatched = await db.Paychecks
            .Where(p => p.PayScheduleId == scheduleId && p.Status == PaycheckStatuses.Expected && p.TransactionId == null)
            .ToListAsync(ct);
        db.Paychecks.RemoveRange(unmatched);
        schedule.NextPayday = PaycheckMatching.FirstPaydayOnOrAfter(schedule.FirstPayday, schedule.Frequency, today.AddDays(-35));
        await db.SaveChangesAsync(ct);

        await RefreshPaychecksAsync(db, schedule, today, ct);
    }

    public async Task DeleteScheduleAsync(int groupId, int scheduleId, CancellationToken ct = default)
    {
        await using var db = await tenantDb.CreateForGroupAsync(groupId, ct);
        var schedule = await db.PaySchedules.FirstOrDefaultAsync(s => s.Id == scheduleId, ct);
        if (schedule is null) return;
        if (Privacy.IsHiddenFrom(schedule, currentUser.UserId))
            throw new ForbiddenAccessException("Only the earner can delete a private paycheck.");

        db.PaySchedules.Remove(schedule); // its paychecks cascade; deposit transactions stay
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdatePaycheckStatusAsync(int groupId, int paycheckId, UpdatePaycheckStatusRequest request, CancellationToken ct = default)
    {
        if (!PaycheckStatuses.IsValid(request.Status))
            throw new ArgumentException($"Unknown paycheck status '{request.Status}'.");

        await using var db = await tenantDb.CreateForGroupAsync(groupId, ct);
        var paycheck = await db.Paychecks.Include(p => p.PaySchedule).FirstOrDefaultAsync(p => p.Id == paycheckId, ct);
        if (paycheck is null) return;
        if (Privacy.IsHiddenFrom(paycheck.PaySchedule, currentUser.UserId))
            throw new ForbiddenAccessException("Only the earner can change a private paycheck.");

        paycheck.Status = request.Status;
        if (request.Status == PaycheckStatuses.Received)
        {
            // Marked by hand (e.g. a paper check): assume the normal amount unless a deposit was matched.
            paycheck.ActualAmount ??= paycheck.ExpectedAmount;
            paycheck.ReceivedAt ??= DateTime.UtcNow;
        }
        else
        {
            paycheck.TransactionId = null;
            paycheck.ActualAmount = null;
            paycheck.ReceivedAt = null;
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task<List<DateTime>> GetPaydaysAsync(int groupId, DateTime from, DateTime to, CancellationToken ct = default)
    {
        await using var db = await tenantDb.CreateForGroupAsync(groupId, ct);
        return await PaydaysAsync(db, from.Date, to.Date, ct);
    }

    public async Task<PayPeriodDto> GetPeriodAsync(int groupId, DateTime date, CancellationToken ct = default)
    {
        await using var db = await tenantDb.CreateForGroupAsync(groupId, ct);
        return await BuildPeriodAsync(db, groupId, currentUser.UserId, date, ct);
    }

    /// <summary>
    /// The pay period containing <paramref name="date"/>, read through a context already scoped to the
    /// group. <paramref name="userId"/> is who's looking (for hiding private details); background jobs
    /// pass an empty id, which sees totals but no private details.
    /// </summary>
    public static async Task<PayPeriodDto> BuildPeriodAsync(ApplicationDbContext db, int groupId, string userId, DateTime date, CancellationToken ct)
    {
        date = date.Date;

        var hasSchedules = await db.PaySchedules.AnyAsync(s => s.IsActive, ct);
        var paydays = await PaydaysAsync(db, date.AddDays(-62), date.AddDays(62), ct);
        var start = paydays.LastOrDefault(d => d <= date, date);
        var next = paydays.FirstOrDefault(d => d > start);
        var end = next != default ? next.AddDays(-1) : start.AddDays(13);
        var names = await MemberNamesAsync(db, groupId, ct);

        // Paychecks landing in the period.
        var paychecks = await db.Paychecks
            .Include(p => p.PaySchedule)
            .Include(p => p.Transaction)
            .Where(p => p.PayDate >= start && p.PayDate <= end)
            .OrderBy(p => p.PayDate).ThenBy(p => p.PaySchedule.Name)
            .ToListAsync(ct);

        var paycheckDtos = paychecks.Select(p =>
        {
            var hidden = Privacy.IsHiddenFrom(p.PaySchedule, userId);
            return new PaycheckDto(
                p.Id, p.PayScheduleId,
                hidden ? Privacy.HiddenPaycheckName : p.PaySchedule.Name,
                names.GetValueOrDefault(p.PaySchedule.UserId, "Former member"),
                p.PayDate,
                hidden ? null : p.ExpectedAmount,
                hidden ? null : p.ActualAmount,
                p.Status, hidden,
                hidden ? null : p.Transaction?.Description);
        }).ToList();

        // Bills paid from this period: moved here by hand, or due after this payday and no later than the
        // next one (so this is the last payday before the due date). Income-type rules are left out
        // (paychecks cover income), and so are bills on cards whose purchases don't count: the card
        // payment from checking is what's budgeted.
        var nextPayday = end.AddDays(1);
        var bills = await db.BillOccurrences
            .Include(b => b.RecurringTransaction).ThenInclude(r => r.Account)
            .Include(b => b.Transaction)
            .Where(b => b.ExpectedAmount > 0
                && b.RecurringTransaction.Account.CountPurchasesInBudget
                && (b.AssignedPayDate != null
                    ? b.AssignedPayDate >= start && b.AssignedPayDate <= end
                    : b.DueDate > start && b.DueDate <= nextPayday))
            .OrderBy(b => b.DueDate).ThenBy(b => b.RecurringTransaction.Payee)
            .ToListAsync(ct);

        var billDtos = bills.Select(b =>
        {
            var hidden = Privacy.IsHiddenFrom(b.RecurringTransaction.Account.PrivateToUserId, userId);
            return new PeriodBillDto(
                b.Id,
                hidden ? Privacy.HiddenBillName : b.RecurringTransaction.Payee,
                b.DueDate,
                hidden ? null : BillAmount(b),
                b.Status, b.AssignedPayDate != null, hidden);
        }).ToList();

        // Everyday spending in the period that isn't a bill payment (those are counted with their bill).
        var budgetStart = await FinanceSettingsService.GetBudgetStartAsync(db, ct);
        var spendFrom = budgetStart is DateTime bs && bs > start ? bs : start;
        var billTransactionIds = db.BillOccurrences.Where(b => b.TransactionId != null).Select(b => b.TransactionId);
        var otherSpending = await db.Transactions
            .Where(t => t.Kind == TransactionKinds.Spending
                && t.Date >= spendFrom && t.Date <= end
                && (t.Account == null || t.Account.CountPurchasesInBudget)
                && !billTransactionIds.Contains(t.Id))
            .SumAsync(t => (decimal?)t.Amount, ct) ?? 0m;

        var counted = paychecks.Where(p => p.Status != PaycheckStatuses.Skipped).ToList();
        var income = counted.Sum(p => p.ActualAmount ?? p.ExpectedAmount);
        var incomeReceived = counted.Where(p => p.Status == PaycheckStatuses.Received).Sum(p => p.ActualAmount ?? p.ExpectedAmount);
        var billsCounted = bills.Where(b => b.Status != BillStatuses.Skipped).ToList();
        var billsTotal = billsCounted.Sum(BillAmount);
        var billsPaid = billsCounted.Where(b => b.Status == BillStatuses.Paid).Sum(BillAmount);

        var incomeStillExpected = counted.Where(p => p.Status == PaycheckStatuses.Expected).Sum(p => p.ExpectedAmount);
        var billsStillDue = billsCounted.Where(b => b.Status == BillStatuses.Expected).Sum(b => b.ExpectedAmount);

        // The previous period's leftover, unless it was moved to savings.
        var carriedIn = await db.LeftoverDecisions
            .Where(d => d.NextPeriodStart == start && d.Decision != LeftoverDecisions.Saved)
            .SumAsync(d => (decimal?)d.Amount, ct) ?? 0m;

        var paydayCount = counted.Select(p => p.PayDate).Distinct().Count();
        var goalsFunding = paydayCount * await GoalsPerPaydayAsync(db, ct);

        return new PayPeriodDto(
            start, end, hasSchedules, paycheckDtos, billDtos,
            income, incomeReceived, billsTotal, billsPaid, otherSpending,
            carriedIn + income - billsTotal - goalsFunding - otherSpending,
            incomeStillExpected, billsStillDue, carriedIn, goalsFunding);
    }

    /// <summary>What every payday sets aside for goals: active goals not yet reached, capped at what each still needs.</summary>
    public static async Task<decimal> GoalsPerPaydayAsync(ApplicationDbContext db, CancellationToken ct)
    {
        var goals = await db.Goals
            .Where(g => g.IsActive && g.PerPaycheckAmount > 0)
            .Select(g => new { g.PerPaycheckAmount, g.TargetAmount, Saved = g.Contributions.Sum(c => (decimal?)c.Amount) ?? 0m })
            .ToListAsync(ct);
        return goals.Sum(g => Math.Min(g.PerPaycheckAmount, Math.Max(0, g.TargetAmount - g.Saved)));
    }

    // --- helpers ---

    /// <summary>A paid bill counts what was actually paid; otherwise the expected amount.</summary>
    private static decimal BillAmount(BillOccurrence b) =>
        b.Status == BillStatuses.Paid && b.Transaction is not null ? b.Transaction.Amount : b.ExpectedAmount;

    private static async Task<List<DateTime>> PaydaysAsync(ApplicationDbContext db, DateTime from, DateTime to, CancellationToken ct) =>
        await db.Paychecks
            .Where(p => p.PayDate >= from && p.PayDate <= to && p.Status != PaycheckStatuses.Skipped)
            .Select(p => p.PayDate)
            .Distinct()
            .OrderBy(d => d)
            .ToListAsync(ct);

    private static async Task RefreshPaychecksAsync(ApplicationDbContext db, PaySchedule schedule, DateTime today, CancellationToken ct)
    {
        if (!schedule.IsActive) return;
        await PaycheckMatching.GenerateForScheduleAsync(db, schedule, today, ct);
        await db.SaveChangesAsync(ct);
        await PaycheckMatching.MatchAsync(db, today, ct);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>UserGroupMember has no tenancy filter, so this filters by group explicitly.</summary>
    internal static async Task<Dictionary<string, string>> MemberNamesAsync(ApplicationDbContext db, int groupId, CancellationToken ct)
    {
        var members = await db.UserGroupMembers
            .Where(m => m.GroupId == groupId)
            .Select(m => new { m.UserId, Name = m.User.UserName })
            .ToListAsync(ct);
        return members
            .GroupBy(m => m.UserId)
            .ToDictionary(g => g.Key, g => g.First().Name ?? "Member");
    }

    private static async Task ValidateAsync(ApplicationDbContext db, SavePayScheduleRequest request,
        Dictionary<string, string> members, string userId, CancellationToken ct)
    {
        if (!members.ContainsKey(request.UserId))
            throw new ArgumentException("The earner must be a member of this group.");
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Name is required.");
        if (request.ExpectedAmount <= 0)
            throw new ArgumentException("The paycheck amount must be more than zero.");
        if (!PaycheckMatching.Frequencies.Contains(request.Frequency))
            throw new ArgumentException("Frequency must be Weekly, BiWeekly or Monthly.");
        if (request.IsPrivate && request.UserId != userId)
            throw new ArgumentException("Only the earner can make their own paycheck private.");

        if (request.DepositAccountId is int accountId)
        {
            var account = await db.Accounts.FirstOrDefaultAsync(a => a.Id == accountId, ct)
                ?? throw new ArgumentException("Deposit account not found.");
            if (Privacy.IsHiddenFrom(account.PrivateToUserId, userId))
                throw new ArgumentException("That account is private to another member.");
        }
    }

    private static void Apply(PaySchedule schedule, SavePayScheduleRequest request)
    {
        schedule.UserId = request.UserId;
        schedule.Name = request.Name.Trim();
        schedule.ExpectedAmount = request.ExpectedAmount;
        schedule.Frequency = request.Frequency;
        schedule.FirstPayday = request.FirstPayday.Date;
        schedule.DepositAccountId = request.DepositAccountId;
        schedule.MatchText = string.IsNullOrWhiteSpace(request.MatchText) ? null : request.MatchText.Trim();
        schedule.IsPrivate = request.IsPrivate;
        schedule.IsActive = request.IsActive;
    }

    private static PayScheduleDto ToDto(PaySchedule s, string userId, Dictionary<string, string> names, DateTime? nextPayday)
    {
        var hidden = Privacy.IsHiddenFrom(s, userId);
        return new PayScheduleDto(
            s.Id, s.UserId, names.GetValueOrDefault(s.UserId, "Former member"),
            hidden ? Privacy.HiddenPaycheckName : s.Name,
            hidden ? null : s.ExpectedAmount,
            s.Frequency, s.FirstPayday,
            hidden ? null : s.DepositAccountId,
            hidden ? null : s.DepositAccount?.Name,
            hidden ? null : s.MatchText,
            s.IsPrivate, s.IsActive,
            IsMine: s.UserId == userId,
            nextPayday);
    }
}
