namespace BBF.Data.Entities;

/// <summary>Which budget alerts one member wants in one group. No row = everything on.</summary>
public class AlertPreferences
{
    public int Id { get; set; }
    public int? GroupId { get; set; }
    public UserGroup? Group { get; set; }
    public string UserId { get; set; } = string.Empty;

    public bool CategoryLimits { get; set; } = true;
    public bool BillsAndBalance { get; set; } = true;
    public bool Payday { get; set; } = true;
    public bool CategorizeReminder { get; set; } = true;

    /// <summary>Off = alerts still appear in the app, just no phone notification.</summary>
    public bool Push { get; set; } = true;
}
