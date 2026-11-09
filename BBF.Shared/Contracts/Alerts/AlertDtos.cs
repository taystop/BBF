namespace BBF.Shared.Contracts.Alerts;

public record FinanceAlertDto(int Id, string Kind, string Title, string Body, string? Link, DateTime CreatedAt);

public record AlertPreferencesDto(
    bool CategoryLimits = true,
    bool BillsAndBalance = true,
    bool Payday = true,
    bool CategorizeReminder = true,
    bool Push = true);

/// <summary>The last pay period ended with money left: move it to savings, or keep it for this period?</summary>
public record LeftoverPromptDto(int Id, DateTime PeriodStart, DateTime PeriodEnd, decimal Amount);

/// <summary>Decision is "Saved" (moved to savings) or "CarriedOver" (keep it for this pay period).</summary>
public record LeftoverDecisionRequest(string Decision);

public interface IAlertService
{
    /// <summary>The signed-in member's alerts from the last 30 days that they haven't cleared, newest first.</summary>
    Task<List<FinanceAlertDto>> GetAlertsAsync(int groupId, CancellationToken ct = default);
    Task DismissAsync(int groupId, int alertId, CancellationToken ct = default);
    Task DismissAllAsync(int groupId, CancellationToken ct = default);

    Task<AlertPreferencesDto> GetPreferencesAsync(int groupId, CancellationToken ct = default);
    Task UpdatePreferencesAsync(int groupId, AlertPreferencesDto preferences, CancellationToken ct = default);

    Task DecideLeftoverAsync(int groupId, int decisionId, LeftoverDecisionRequest request, CancellationToken ct = default);
}
