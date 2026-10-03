using BBF.Api.Auth;
using BBF.Data;
using BBF.Data.Entities;
using BBF.Shared.Contracts.Auth;
using BBF.Shared.Contracts.Groups;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BBF.Api.Services;

/// <summary>
/// Backs the Admin-only group management page (create/rename/delete a group, add/remove/re-role
/// members) — gated by the "AdminOnly" authorization policy at the endpoint level, matching the
/// original page's [Authorize(Roles = "Admin")]. Also hardens something the original UI only
/// enforced client-side: per-group "Owner" checks on mutating actions were never actually verified
/// server-side before (any authenticated user who reached the page's code path could have called
/// these without being an Owner) — this version checks it for real.
/// </summary>
public class GroupManagementService(IDbContextFactory<ApplicationDbContext> dbFactory, UserManager<ApplicationUser> userManager)
{
    public async Task<List<GroupDto>> GetMyGroupsAsync(string userId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var groups = await GetOrCreateGroupsForUserAsync(db, userId, ct);
        return groups.Select(g => new GroupDto(g.Id, g.Name)).ToList();
    }

    public async Task<List<GroupDetailDto>> GetAllGroupsDetailedAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var groups = await db.UserGroups
            .Include(g => g.Members).ThenInclude(m => m.User)
            .OrderBy(g => g.Name)
            .ToListAsync(ct);

        return groups.Select(ToDetailDto).ToList();
    }

    public async Task<GroupDto> CreateGroupAsync(string ownerUserId, CreateGroupRequest request, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var group = new UserGroup { Name = request.Name, CreatedAt = DateTime.UtcNow };
        db.UserGroups.Add(group);
        await db.SaveChangesAsync(ct);

        db.UserGroupMembers.Add(new UserGroupMember
        {
            GroupId = group.Id,
            UserId = ownerUserId,
            Role = "Owner",
            JoinedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(ct);

        return new GroupDto(group.Id, group.Name);
    }

    public async Task UpdateGroupAsync(string callerUserId, int groupId, UpdateGroupRequest request, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (!await IsOwnerAsync(db, groupId, callerUserId, ct)) throw new ForbiddenAccessException("Only a group owner can rename it.");

        var entity = await db.UserGroups.FirstOrDefaultAsync(g => g.Id == groupId, ct);
        if (entity is null) return;

        entity.Name = request.Name;
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteGroupAsync(string callerUserId, int groupId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (!await IsOwnerAsync(db, groupId, callerUserId, ct)) throw new ForbiddenAccessException("Only a group owner can delete it.");

        var entity = await db.UserGroups.FirstOrDefaultAsync(g => g.Id == groupId, ct);
        if (entity is null) return;

        db.UserGroups.Remove(entity);
        await db.SaveChangesAsync(ct);
    }

    public async Task<GroupMemberDto> AddMemberAsync(string callerUserId, int groupId, AddGroupMemberRequest request, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (!await IsOwnerAsync(db, groupId, callerUserId, ct)) throw new ForbiddenAccessException("Only a group owner can add members.");

        var email = request.Email.Trim();
        var user = await userManager.FindByEmailAsync(email)
            ?? throw new KeyNotFoundException($"No user found with email '{email}'.");

        if (await db.UserGroupMembers.AnyAsync(m => m.GroupId == groupId && m.UserId == user.Id, ct))
            throw new InvalidOperationException("User is already a member of this group.");

        var member = new UserGroupMember { GroupId = groupId, UserId = user.Id, Role = "Member", JoinedAt = DateTime.UtcNow };
        db.UserGroupMembers.Add(member);
        await db.SaveChangesAsync(ct);

        return new GroupMemberDto(member.Id, user.Id, user.UserName ?? email, member.Role, member.JoinedAt);
    }

    public async Task RemoveMemberAsync(string callerUserId, int groupId, int memberId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (!await IsOwnerAsync(db, groupId, callerUserId, ct)) throw new ForbiddenAccessException("Only a group owner can remove members.");

        var entity = await db.UserGroupMembers.FirstOrDefaultAsync(m => m.Id == memberId && m.GroupId == groupId, ct);
        if (entity is null) return;

        db.UserGroupMembers.Remove(entity);
        await db.SaveChangesAsync(ct);
    }

    public async Task ChangeMemberRoleAsync(string callerUserId, int groupId, int memberId, ChangeGroupMemberRoleRequest request, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (!await IsOwnerAsync(db, groupId, callerUserId, ct)) throw new ForbiddenAccessException("Only a group owner can change member roles.");

        var entity = await db.UserGroupMembers.FirstOrDefaultAsync(m => m.Id == memberId && m.GroupId == groupId, ct);
        if (entity is null) return;

        entity.Role = request.Role;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Loads the user's groups, auto-creating a personal one on first call if they have none — ported from the old UserContextService.</summary>
    public static async Task<List<UserGroup>> GetOrCreateGroupsForUserAsync(ApplicationDbContext db, string userId, CancellationToken ct)
    {
        var groups = await db.UserGroupMembers
            .Where(m => m.UserId == userId)
            .Include(m => m.Group)
            .Select(m => m.Group)
            .OrderBy(g => g.Name)
            .ToListAsync(ct);

        if (groups.Count > 0) return groups;

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

        return [personalGroup];
    }

    private static async Task<bool> IsOwnerAsync(ApplicationDbContext db, int groupId, string userId, CancellationToken ct) =>
        await db.UserGroupMembers.AnyAsync(m => m.GroupId == groupId && m.UserId == userId && m.Role == "Owner", ct);

    private static GroupDetailDto ToDetailDto(UserGroup g) => new(
        g.Id, g.Name, g.CreatedAt,
        g.Members.Select(m => new GroupMemberDto(m.Id, m.UserId, m.User?.UserName ?? "Unknown", m.Role, m.JoinedAt)).ToList());
}
