using System.Net.Http.Json;
using BBF.Shared.Contracts.Auth;
using BBF.Shared.Contracts.Groups;

namespace BBF.Shared.ApiClients;

public class HttpGroupService(HttpClient http) : IGroupService
{
    public async Task<List<GroupDto>> GetMyGroupsAsync(CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<GroupDto>>("/api/groups", ct) ?? [];

    public async Task<List<GroupDetailDto>> GetAllGroupsDetailedAsync(CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<GroupDetailDto>>("/api/admin/groups", ct) ?? [];

    public async Task<GroupDto> CreateGroupAsync(CreateGroupRequest request, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync("/api/admin/groups", request, ct);
        await response.EnsureSuccessWithErrorBodyAsync(ct);
        return await response.Content.ReadFromJsonAsync<GroupDto>(ct)
            ?? throw new InvalidOperationException("Empty response creating group.");
    }

    public async Task UpdateGroupAsync(int groupId, UpdateGroupRequest request, CancellationToken ct = default)
    {
        var response = await http.PutAsJsonAsync($"/api/admin/groups/{groupId}", request, ct);
        await response.EnsureSuccessWithErrorBodyAsync(ct);
    }

    public async Task DeleteGroupAsync(int groupId, CancellationToken ct = default)
    {
        var response = await http.DeleteAsync($"/api/admin/groups/{groupId}", ct);
        await response.EnsureSuccessWithErrorBodyAsync(ct);
    }

    public async Task<GroupMemberDto> AddMemberAsync(int groupId, AddGroupMemberRequest request, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync($"/api/admin/groups/{groupId}/members", request, ct);
        await response.EnsureSuccessWithErrorBodyAsync(ct);
        return await response.Content.ReadFromJsonAsync<GroupMemberDto>(ct)
            ?? throw new InvalidOperationException("Empty response adding group member.");
    }

    public async Task RemoveMemberAsync(int groupId, int memberId, CancellationToken ct = default)
    {
        var response = await http.DeleteAsync($"/api/admin/groups/{groupId}/members/{memberId}", ct);
        await response.EnsureSuccessWithErrorBodyAsync(ct);
    }

    public async Task ChangeMemberRoleAsync(int groupId, int memberId, ChangeGroupMemberRoleRequest request, CancellationToken ct = default)
    {
        var response = await http.PutAsJsonAsync($"/api/admin/groups/{groupId}/members/{memberId}/role", request, ct);
        await response.EnsureSuccessWithErrorBodyAsync(ct);
    }
}
