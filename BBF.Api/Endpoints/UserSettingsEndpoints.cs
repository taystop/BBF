using BBF.Api.Auth;
using BBF.Data;
using BBF.Data.Entities;
using BBF.Shared.Contracts.Settings;
using Microsoft.EntityFrameworkCore;

namespace BBF.Api.Endpoints;

/// <summary>The signed-in user's own settings. Always keyed by the token's user id, never a route value.</summary>
public static class UserSettingsEndpoints
{
    public static void MapUserSettingsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/me").RequireAuthorization();

        group.MapGet("/notification-settings", async (ICurrentUserContext currentUser, IDbContextFactory<ApplicationDbContext> dbFactory, CancellationToken ct) =>
        {
            var userId = currentUser.UserId;
            await using var db = await dbFactory.CreateDbContextAsync(ct);
            var settings = await db.UserNotificationSettings.FirstOrDefaultAsync(s => s.UserId == userId, ct);
            return Results.Ok(new NotificationSettingsDto(settings?.ServiceAlerts ?? true));
        });

        group.MapPut("/notification-settings", async (NotificationSettingsDto request, ICurrentUserContext currentUser, IDbContextFactory<ApplicationDbContext> dbFactory, CancellationToken ct) =>
        {
            var userId = currentUser.UserId;
            await using var db = await dbFactory.CreateDbContextAsync(ct);
            var settings = await db.UserNotificationSettings.FirstOrDefaultAsync(s => s.UserId == userId, ct);
            if (settings is null)
            {
                settings = new UserNotificationSettings { UserId = userId };
                db.UserNotificationSettings.Add(settings);
            }
            settings.ServiceAlerts = request.ServiceAlerts;
            settings.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });
    }
}
