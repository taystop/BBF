namespace BBF.Data.Entities;

/// <summary>An FCM registration token for one device. A user can have several (phone, tablet, ...);
/// a token is re-upserted (not duplicated) if the same device registers again, including under a
/// different user after a re-login.</summary>
public class DeviceToken
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;
}
