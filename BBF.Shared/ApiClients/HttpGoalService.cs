using System.Net.Http.Json;
using BBF.Shared.Contracts.Goals;

namespace BBF.Shared.ApiClients;

public class HttpGoalService(HttpClient http) : IGoalService
{
    public async Task<List<GoalDto>> GetGoalsAsync(int groupId, CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<GoalDto>>($"/api/groups/{groupId}/goals", ct) ?? [];

    public async Task<GoalDto> CreateAsync(int groupId, CreateGoalRequest request, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync($"/api/groups/{groupId}/goals", request, ct);
        await response.EnsureSuccessWithErrorBodyAsync(ct);
        return await response.Content.ReadFromJsonAsync<GoalDto>(ct)
            ?? throw new InvalidOperationException("Empty response creating goal.");
    }

    public async Task UpdateAsync(int groupId, int goalId, UpdateGoalRequest request, CancellationToken ct = default)
    {
        var response = await http.PutAsJsonAsync($"/api/groups/{groupId}/goals/{goalId}", request, ct);
        await response.EnsureSuccessWithErrorBodyAsync(ct);
    }

    public async Task SetActiveAsync(int groupId, int goalId, bool isActive, CancellationToken ct = default)
    {
        var response = await http.PostAsync($"/api/groups/{groupId}/goals/{goalId}/active?isActive={isActive}", null, ct);
        await response.EnsureSuccessWithErrorBodyAsync(ct);
    }

    public async Task AddContributionAsync(int groupId, int goalId, AddGoalContributionRequest request, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync($"/api/groups/{groupId}/goals/{goalId}/contributions", request, ct);
        await response.EnsureSuccessWithErrorBodyAsync(ct);
    }
}
