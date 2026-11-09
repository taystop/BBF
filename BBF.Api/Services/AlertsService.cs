using BBF.Api.Auth;
using BBF.Data.Entities;
using BBF.Shared.Contracts.Alerts;
using Microsoft.EntityFrameworkCore;

namespace BBF.Api.Services;

/// <summary>A member's own budget alerts and alert settings, and the leftover-to-savings decision.</summary>
public class AlertsService(ITenantDbContextFactory tenantDb, ICurrentUserContext currentUser)
{
    public async Task<List<FinanceAlertDto>> GetAlertsAsync(int groupId, CancellationToken ct = default)
    {
        await using var db = await tenantDb.CreateForGroupAsync(groupId, ct);
        var userId = currentUser.UserId;
        var since = DateTime.UtcNow.AddDays(-30);
        return await db.FinanceAlerts
            .Where(a => a.UserId == userId && a.DismissedAt == null && a.CreatedAt >= since)
            .OrderByDescending(a => a.CreatedAt)
            .Take(50)
            .Select(a => new FinanceAlertDto(a.Id, a.Kind, a.Title, a.Body, a.Link, a.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task DismissAsync(int groupId, int alertId, CancellationToken ct = default)
    {
        await using var db = await tenantDb.CreateForGroupAsync(groupId, ct);
        var userId = currentUser.UserId;
        await db.FinanceAlerts
            .Where(a => a.Id == alertId && a.UserId == userId && a.DismissedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.DismissedAt, DateTime.UtcNow), ct);
    }

    public async Task DismissAllAsync(int groupId, CancellationToken ct = default)
    {
        await using var db = await tenantDb.CreateForGroupAsync(groupId, ct);
        var userId = currentUser.UserId;
        await db.FinanceAlerts
            .Where(a => a.UserId == userId && a.DismissedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.DismissedAt, DateTime.UtcNow), ct);
    }

    public async Task<AlertPreferencesDto> GetPreferencesAsync(int groupId, CancellationToken ct = default)
    {
        await using var db = await tenantDb.CreateForGroupAsync(groupId, ct);
        var userId = currentUser.UserId;
        var p = await db.AlertPreferences.FirstOrDefaultAsync(x => x.UserId == userId, ct);
        return p is null
            ? new AlertPreferencesDto()
            : new AlertPreferencesDto(p.CategoryLimits, p.BillsAndBalance, p.Payday, p.CategorizeReminder, p.Push);
    }

    public async Task UpdatePreferencesAsync(int groupId, AlertPreferencesDto request, CancellationToken ct = default)
    {
        await using var db = await tenantDb.CreateForGroupAsync(groupId, ct);
        var userId = currentUser.UserId;
        var p = await db.AlertPreferences.FirstOrDefaultAsync(x => x.UserId == userId, ct);
        if (p is null)
        {
            p = new AlertPreferences { GroupId = groupId, UserId = userId };
            db.AlertPreferences.Add(p);
        }
        p.CategoryLimits = request.CategoryLimits;
        p.BillsAndBalance = request.BillsAndBalance;
        p.Payday = request.Payday;
        p.CategorizeReminder = request.CategorizeReminder;
        p.Push = request.Push;
        await db.SaveChangesAsync(ct);
    }

    public async Task DecideLeftoverAsync(int groupId, int decisionId, LeftoverDecisionRequest request, CancellationToken ct = default)
    {
        if (request.Decision is not (LeftoverDecisions.Saved or LeftoverDecisions.CarriedOver))
            throw new ArgumentException("Decision must be Saved or CarriedOver.");

        await using var db = await tenantDb.CreateForGroupAsync(groupId, ct);
        var decision = await db.LeftoverDecisions.FirstOrDefaultAsync(d => d.Id == decisionId, ct);
        if (decision is null) return;

        decision.Decision = request.Decision;
        decision.DecidedAt = DateTime.UtcNow;
        decision.DecidedByUserId = currentUser.UserId;

        // The question is answered for everyone: clear the matching alert from every member's list.
        var key = AlertKeys.Leftover(decision.PeriodStart);
        await db.FinanceAlerts.Where(a => a.Key == key && a.DismissedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.DismissedAt, DateTime.UtcNow), ct);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>The open "move it to savings?" question for the current pay period, if any.</summary>
    public static async Task<LeftoverPromptDto?> GetLeftoverPromptAsync(BBF.Data.ApplicationDbContext db, DateTime currentPeriodStart, CancellationToken ct) =>
        await db.LeftoverDecisions
            .Where(d => d.NextPeriodStart == currentPeriodStart && d.Decision == LeftoverDecisions.Pending && d.Amount > 0)
            .Select(d => new LeftoverPromptDto(d.Id, d.PeriodStart, d.PeriodEnd, d.Amount))
            .FirstOrDefaultAsync(ct);
}

/// <summary>Alert keys, so each alert happens once.</summary>
public static class AlertKeys
{
    public static string Leftover(DateTime periodStart) => $"leftover:{periodStart:yyyyMMdd}";
    public static string Payday(DateTime payday) => $"payday:{payday:yyyyMMdd}";
    public static string Bill(int billId) => $"bill:{billId}";
    public static string LowBalance(DateTime date) => $"lowbal:{date:yyyyMMdd}";
    public static string Category(int categoryId, DateTime month, int percent) => $"cat:{categoryId}:{month:yyyyMM}:{percent}";
    public static string Categorize(DateTime date) => $"categorize:{date:yyyyMMdd}";
}
