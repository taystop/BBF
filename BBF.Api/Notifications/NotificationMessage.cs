namespace BBF.Api.Notifications;

/// <summary>
/// A push notification. Category lets each user turn a kind of broadcast off (see
/// <see cref="NotificationCategories"/>); Data["link"] is the app page a tap opens.
/// </summary>
public record NotificationMessage(
    string Title,
    string Body,
    IReadOnlyDictionary<string, string>? Data = null,
    string? Category = null);

public static class NotificationCategories
{
    /// <summary>Service down / back up. Users opt out in Settings (UserNotificationSettings.ServiceAlerts).</summary>
    public const string Services = "services";
}
