namespace BBF.Data.Entities;

/// <summary>
/// One budget alert for one member: shown in the app's alert list and pushed to their phone. Key makes
/// each alert happen once (e.g. "bill:42" or "cat:7:2026-11:100").
/// </summary>
public class FinanceAlert
{
    public int Id { get; set; }
    public int? GroupId { get; set; }
    public UserGroup? Group { get; set; }

    /// <summary>The member it's for. Alerts about a private item go only to its owner.</summary>
    public string UserId { get; set; } = string.Empty;

    public string Kind { get; set; } = string.Empty; // see FinanceAlertKinds
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;

    /// <summary>App page the alert opens, e.g. "/money".</summary>
    public string? Link { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DismissedAt { get; set; }
}

public static class FinanceAlertKinds
{
    public const string CategoryLimit = "CategoryLimit";
    public const string BillDue = "BillDue";
    public const string LowBalance = "LowBalance";
    public const string Payday = "Payday";
    public const string Categorize = "Categorize";
    public const string Leftover = "Leftover";
}
