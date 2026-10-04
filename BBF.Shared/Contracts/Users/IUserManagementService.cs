namespace BBF.Shared.Contracts.Users;

/// <summary>Admin-only Identity user/role management — matches the original Users.razor page's [Authorize(Roles = "Admin")] gate.</summary>
public interface IUserManagementService
{
    Task<List<UserSummaryDto>> GetAllUsersAsync(CancellationToken ct = default);
    Task<List<string>> GetAllRolesAsync(CancellationToken ct = default);
    Task CreateUserAsync(CreateUserRequest request, CancellationToken ct = default);
    Task UpdateUserAsync(string userId, UpdateUserRequest request, CancellationToken ct = default);
    Task DeleteUserAsync(string userId, CancellationToken ct = default);
    Task CreateRoleAsync(string roleName, CancellationToken ct = default);
    Task DeleteRoleAsync(string roleName, CancellationToken ct = default);
}
