using BBF.Api.Auth;
using BBF.Data;
using BBF.Data.Entities;
using BBF.Shared.Contracts.Auth;
using Microsoft.EntityFrameworkCore;

namespace BBF.Api.Endpoints;

public static class GroupsEndpoints
{
    public static void MapGroupsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/groups").RequireAuthorization();

        group.MapGet("/", async (
            ICurrentUserContext currentUser,
            IDbContextFactory<ApplicationDbContext> dbFactory,
            CancellationToken ct) =>
        {
            var userId = currentUser.UserId;
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            var groups = await db.UserGroupMembers
                .Where(m => m.UserId == userId)
                .Include(m => m.Group)
                .Select(m => m.Group)
                .OrderBy(g => g.Name)
                .ToListAsync(ct);

            // Auto-create a personal group on first login — ported from the old UserContextService,
            // minus the recurring-transaction materialization side effect (that moves to a proper
            // BackgroundService alongside the rest of the budget domain, not a client-triggered call).
            if (groups.Count == 0)
            {
                var user = await db.Users.FindAsync([userId], ct);
                var displayName = user?.UserName ?? "User";

                var personalGroup = new UserGroup { Name = $"{displayName}'s Budget", CreatedAt = DateTime.UtcNow };
                db.UserGroups.Add(personalGroup);
                await db.SaveChangesAsync(ct);

                db.UserGroupMembers.Add(new UserGroupMember
                {
                    GroupId = personalGroup.Id,
                    UserId = userId,
                    Role = "Owner",
                    JoinedAt = DateTime.UtcNow
                });
                await db.SaveChangesAsync(ct);

                groups = [personalGroup];
            }

            return Results.Ok(groups.Select(g => new GroupDto(g.Id, g.Name)).ToList());
        });
    }
}
