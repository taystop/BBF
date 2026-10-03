using BBF.Shared.Contracts.Groups;

namespace BBF.Shared.Contracts.Auth;

public interface IAuthService
{
    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
}

public interface IGroupService
{
    /// <summary>Lists the signed-in user's groups, auto-creating a personal group on first call if they have none.</summary>
    Task<List<GroupDto>> GetMyGroupsAsync(CancellationToken ct = default);

    // --- Group management (server-enforced Admin-role gate, matching the original page's
    // [Authorize(Roles = "Admin")] attribute) ---

    Task<List<GroupDetailDto>> GetAllGroupsDetailedAsync(CancellationToken ct = default);
    Task<GroupDto> CreateGroupAsync(CreateGroupRequest request, CancellationToken ct = default);
    Task UpdateGroupAsync(int groupId, UpdateGroupRequest request, CancellationToken ct = default);
    Task DeleteGroupAsync(int groupId, CancellationToken ct = default);
    Task<GroupMemberDto> AddMemberAsync(int groupId, AddGroupMemberRequest request, CancellationToken ct = default);
    Task RemoveMemberAsync(int groupId, int memberId, CancellationToken ct = default);
    Task ChangeMemberRoleAsync(int groupId, int memberId, ChangeGroupMemberRoleRequest request, CancellationToken ct = default);
}
