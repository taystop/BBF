using System.Security.Claims;
using BBF.Data;
using Microsoft.EntityFrameworkCore;

namespace BBF.Api.Auth;

public class CurrentUserContext(IHttpContextAccessor httpContextAccessor, IDbContextFactory<ApplicationDbContext> dbFactory)
    : ICurrentUserContext
{
    public string UserId =>
        httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("No authenticated user on the current request.");

    public async Task<int> GetVerifiedGroupIdAsync(int requestedGroupId, CancellationToken ct = default)
    {
        var userId = UserId;

        // UserGroupMember carries no tenancy query filter itself (it IS the membership table), so this
        // check is always evaluated against the full table regardless of any ambient CurrentGroupId.
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var isMember = await db.UserGroupMembers
            .AnyAsync(m => m.UserId == userId && m.GroupId == requestedGroupId, ct);

        if (!isMember)
            throw new ForbiddenAccessException($"User {userId} is not a member of group {requestedGroupId}.");

        return requestedGroupId;
    }
}
