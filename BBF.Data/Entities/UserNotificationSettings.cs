namespace BBF.Data.Entities;

/// <summary>
/// One user's choices for notifications that aren't tied to a budget group (those live in
/// <see cref="AlertPreferences"/>, per group). No row = everything on.
/// </summary>
public class UserNotificationSettings
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;

    /// <summary>"Service down" / "Service back up" pushes from the service health monitor.</summary>
    public bool ServiceAlerts { get; set; } = true;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
