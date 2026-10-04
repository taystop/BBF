using System.Net.Http.Json;
using BBF.Shared.Contracts.Users;

namespace BBF.ApiClients;

public class HttpUserManagementService(HttpClient http) : IUserManagementService
{
    public async Task<List<UserSummaryDto>> GetAllUsersAsync(CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<UserSummaryDto>>("/api/admin/users", ct) ?? [];

    public async Task<List<string>> GetAllRolesAsync(CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<string>>("/api/admin/users/roles", ct) ?? [];

    public async Task CreateUserAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync("/api/admin/users", request, ct);
        await response.EnsureSuccessWithErrorBodyAsync(ct);
    }

    public async Task UpdateUserAsync(string userId, UpdateUserRequest request, CancellationToken ct = default)
    {
        var response = await http.PutAsJsonAsync($"/api/admin/users/{userId}", request, ct);
        await response.EnsureSuccessWithErrorBodyAsync(ct);
    }

    public async Task DeleteUserAsync(string userId, CancellationToken ct = default)
    {
        var response = await http.DeleteAsync($"/api/admin/users/{userId}", ct);
        await response.EnsureSuccessWithErrorBodyAsync(ct);
    }

    public async Task CreateRoleAsync(string roleName, CancellationToken ct = default)
    {
        var response = await http.PostAsync($"/api/admin/users/roles/{Uri.EscapeDataString(roleName)}", null, ct);
        await response.EnsureSuccessWithErrorBodyAsync(ct);
    }

    public async Task DeleteRoleAsync(string roleName, CancellationToken ct = default)
    {
        var response = await http.DeleteAsync($"/api/admin/users/roles/{Uri.EscapeDataString(roleName)}", ct);
        await response.EnsureSuccessWithErrorBodyAsync(ct);
    }
}
