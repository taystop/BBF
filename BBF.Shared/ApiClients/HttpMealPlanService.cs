using System.Net.Http.Json;
using BBF.Shared.Contracts.MealPlanning;

namespace BBF.Shared.ApiClients;

public class HttpMealPlanService(HttpClient http) : IMealPlanService
{
    public async Task<MealPlanWeekDto> GetOrCreateWeekAsync(int groupId, DateTime weekStartDate, CancellationToken ct = default)
    {
        var weekStart = Uri.EscapeDataString(weekStartDate.ToString("yyyy-MM-dd"));
        var result = await http.GetFromJsonAsync<MealPlanWeekDto>(
            $"/api/groups/{groupId}/meal-plan/weeks?weekStart={weekStart}", ct);
        return result ?? throw new InvalidOperationException("Empty response loading meal plan week.");
    }

    public async Task AssignRecipeAsync(int groupId, int weekId, AssignRecipeRequest request, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync($"/api/groups/{groupId}/meal-plan/weeks/{weekId}/entries", request, ct);
        await response.EnsureSuccessWithErrorBodyAsync(ct);
    }

    public async Task UnassignAsync(int groupId, int entryId, CancellationToken ct = default)
    {
        var response = await http.DeleteAsync($"/api/groups/{groupId}/meal-plan/entries/{entryId}", ct);
        await response.EnsureSuccessWithErrorBodyAsync(ct);
    }

    public async Task<List<RecipeDto>> GetRecentlyUsedRecipesAsync(int groupId, int excludeWeekId, int take = 10, CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<RecipeDto>>(
            $"/api/groups/{groupId}/meal-plan/weeks/{excludeWeekId}/recently-used-recipes?take={take}", ct) ?? [];
}
