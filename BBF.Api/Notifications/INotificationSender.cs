namespace BBF.Api.Notifications;

/// <summary>
/// The one thing any future trigger (service-down alerts, budget alerts, chat replies, ...) needs to
/// depend on to send a push notification — nothing about FCM, device tokens, or delivery mechanics
/// leaks past this interface. A different provider, or a no-op for local dev, slots in by swapping
/// the DI registration in Program.cs — no call site changes.
/// </summary>
public interface INotificationSender
{
    Task SendToUserAsync(string userId, NotificationMessage message, CancellationToken ct = default);

    Task SendToUsersAsync(IEnumerable<string> userIds, NotificationMessage message, CancellationToken ct = default);

    /// <summary>Every registered device, regardless of owner — for global/household-wide alerts (e.g. a
    /// monitored service going down) where there's no single "owning" user or group to target.</summary>
    Task SendToAllAsync(NotificationMessage message, CancellationToken ct = default);
}
