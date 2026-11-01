using BBF.Data;
using BBF.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace BBF.Api.Services;

public class DeviceTokenService(IDbContextFactory<ApplicationDbContext> dbFactory)
{
    public async Task RegisterAsync(string userId, string token, string platform, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var existing = await db.DeviceTokens.FirstOrDefaultAsync(t => t.Token == token, ct);

        if (existing is null)
        {
            db.DeviceTokens.Add(new DeviceToken { UserId = userId, Token = token, Platform = platform });
        }
        else
        {
            // Re-registering (app restart, or a different user signed in on the same device) —
            // reassign ownership and refresh LastSeenAt rather than insert a duplicate row.
            existing.UserId = userId;
            existing.Platform = platform;
            existing.LastSeenAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task UnregisterAsync(string userId, string token, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await db.DeviceTokens.Where(t => t.Token == token && t.UserId == userId).ExecuteDeleteAsync(ct);
    }
}
