using BBF.Api.Auth;
using BBF.Api.Services;
using BBF.Shared.Contracts.Auth;
using BBF.Shared.Contracts.Groups;

namespace BBF.Api.Endpoints;

public static class GroupsEndpoints
{
    public static void MapGroupsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/groups").RequireAuthorization();

        // Auto-creates a personal group on first login — ported from the old UserContextService,
        // minus the recurring-transaction materialization side effect (that's now a proper
        // BackgroundService, not a client-triggered call — see Phase 1b).
        group.MapGet("/", async (ICurrentUserContext currentUser, GroupManagementService groups, CancellationToken ct) =>
            Results.Ok(await groups.GetMyGroupsAsync(currentUser.UserId, ct)));

        // Admin-only group management — matches the original Groups.razor page's
        // [Authorize(Roles = "Admin")] gate. Per-group Owner checks happen inside the service itself.
        var admin = app.MapGroup("/api/admin/groups").RequireAuthorization("AdminOnly");

        admin.MapGet("/", async (GroupManagementService groups, CancellationToken ct) =>
            Results.Ok(await groups.GetAllGroupsDetailedAsync(ct)));

        admin.MapPost("/", async (ICurrentUserContext currentUser, CreateGroupRequest request, GroupManagementService groups, CancellationToken ct) =>
            Results.Ok(await groups.CreateGroupAsync(currentUser.UserId, request, ct)));

        admin.MapPut("/{groupId:int}", async (ICurrentUserContext currentUser, int groupId, UpdateGroupRequest request, GroupManagementService groups, CancellationToken ct) =>
        {
            await groups.UpdateGroupAsync(currentUser.UserId, groupId, request, ct);
            return Results.NoContent();
        });

        admin.MapDelete("/{groupId:int}", async (ICurrentUserContext currentUser, int groupId, GroupManagementService groups, CancellationToken ct) =>
        {
            await groups.DeleteGroupAsync(currentUser.UserId, groupId, ct);
            return Results.NoContent();
        });

        admin.MapPost("/{groupId:int}/members", async (ICurrentUserContext currentUser, int groupId, AddGroupMemberRequest request, GroupManagementService groups, CancellationToken ct) =>
            Results.Ok(await groups.AddMemberAsync(currentUser.UserId, groupId, request, ct)));

        admin.MapDelete("/{groupId:int}/members/{memberId:int}", async (ICurrentUserContext currentUser, int groupId, int memberId, GroupManagementService groups, CancellationToken ct) =>
        {
            await groups.RemoveMemberAsync(currentUser.UserId, groupId, memberId, ct);
            return Results.NoContent();
        });

        admin.MapPut("/{groupId:int}/members/{memberId:int}/role", async (ICurrentUserContext currentUser, int groupId, int memberId, ChangeGroupMemberRoleRequest request, GroupManagementService groups, CancellationToken ct) =>
        {
            await groups.ChangeMemberRoleAsync(currentUser.UserId, groupId, memberId, request, ct);
            return Results.NoContent();
        });
    }
}
