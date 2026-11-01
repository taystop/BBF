namespace BBF.Api.Notifications;

/// <summary>
/// Used until Firebase:ServiceAccountPath is actually configured (see Program.cs) — lets every
/// trigger call through INotificationSender unconditionally, without BBF.Api crashing on startup or
/// every call site needing its own "is this set up yet" check.
/// </summary>
public class NullNotificationSender(ILogger<NullNotificationSender> logger) : INotificationSender
{
    public Task SendToUserAsync(string userId, NotificationMessage message, CancellationToken ct = default)
    {
        logger.LogInformation("Push not configured — would have sent \"{Title}\" to user {UserId}", message.Title, userId);
        return Task.CompletedTask;
    }

    public Task SendToUsersAsync(IEnumerable<string> userIds, NotificationMessage message, CancellationToken ct = default)
    {
        logger.LogInformation("Push not configured — would have sent \"{Title}\" to {Count} user(s)", message.Title, userIds.Count());
        return Task.CompletedTask;
    }

    public Task SendToAllAsync(NotificationMessage message, CancellationToken ct = default)
    {
        logger.LogInformation("Push not configured — would have broadcast \"{Title}\"", message.Title);
        return Task.CompletedTask;
    }
}
