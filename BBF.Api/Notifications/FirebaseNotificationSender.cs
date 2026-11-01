using BBF.Data;
using FirebaseAdmin.Messaging;
using Microsoft.EntityFrameworkCore;

namespace BBF.Api.Notifications;

public class FirebaseNotificationSender(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    ILogger<FirebaseNotificationSender> logger) : INotificationSender
{
    public Task SendToUserAsync(string userId, NotificationMessage message, CancellationToken ct = default) =>
        SendToUsersAsync([userId], message, ct);

    public async Task SendToUsersAsync(IEnumerable<string> userIds, NotificationMessage message, CancellationToken ct = default)
    {
        var userIdSet = userIds.ToHashSet();
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var tokens = await db.DeviceTokens.Where(t => userIdSet.Contains(t.UserId)).Select(t => t.Token).ToListAsync(ct);
        await SendAsync(tokens, message, ct);
    }

    public async Task SendToAllAsync(NotificationMessage message, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var tokens = await db.DeviceTokens.Select(t => t.Token).ToListAsync(ct);
        await SendAsync(tokens, message, ct);
    }

    private async Task SendAsync(List<string> tokens, NotificationMessage message, CancellationToken ct)
    {
        if (tokens.Count == 0) return;

        var multicast = new MulticastMessage
        {
            Tokens = tokens,
            Notification = new Notification { Title = message.Title, Body = message.Body },
            Data = message.Data,
        };

        BatchResponse response;
        try
        {
            response = await FirebaseMessaging.DefaultInstance.SendEachForMulticastAsync(multicast, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send push notification \"{Title}\"", message.Title);
            return;
        }

        if (response.FailureCount == 0) return;

        // A device that uninstalled the app or had FCM reissue its token shows up as "Unregistered" —
        // prune those rows so later sends don't keep paying for a doomed delivery attempt.
        var deadTokens = response.Responses
            .Select((result, i) => (result, token: tokens[i]))
            .Where(x => !x.result.IsSuccess && x.result.Exception?.MessagingErrorCode == MessagingErrorCode.Unregistered)
            .Select(x => x.token)
            .ToList();

        if (deadTokens.Count == 0) return;

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await db.DeviceTokens.Where(t => deadTokens.Contains(t.Token)).ExecuteDeleteAsync(ct);
        logger.LogInformation("Pruned {Count} unregistered device token(s)", deadTokens.Count);
    }
}
