using System.Net.Http.Json;
using BBF.Shared.Contracts.MealPlanning;

namespace BBF.ApiClients;

public class HttpRecipeService(HttpClient http) : IRecipeService
{
    public async Task<List<RecipeDto>> GetRecipesAsync(int groupId, CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<RecipeDto>>($"/api/groups/{groupId}/recipes", ct) ?? [];

    public async Task<RecipeDto?> GetRecipeAsync(int groupId, int recipeId, CancellationToken ct = default)
    {
        var response = await http.GetAsync($"/api/groups/{groupId}/recipes/{recipeId}", ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        await response.EnsureSuccessWithErrorBodyAsync(ct);
        return await response.Content.ReadFromJsonAsync<RecipeDto>(ct);
    }

    public async Task<RecipeDto> CreateAsync(int groupId, CreateRecipeRequest request, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync($"/api/groups/{groupId}/recipes", request, ct);
        await response.EnsureSuccessWithErrorBodyAsync(ct);
        return await response.Content.ReadFromJsonAsync<RecipeDto>(ct)
            ?? throw new InvalidOperationException("Empty response creating recipe.");
    }

    public async Task UpdateAsync(int groupId, int recipeId, UpdateRecipeRequest request, CancellationToken ct = default)
    {
        var response = await http.PutAsJsonAsync($"/api/groups/{groupId}/recipes/{recipeId}", request, ct);
        await response.EnsureSuccessWithErrorBodyAsync(ct);
    }

    public async Task DeleteAsync(int groupId, int recipeId, CancellationToken ct = default)
    {
        var response = await http.DeleteAsync($"/api/groups/{groupId}/recipes/{recipeId}", ct);
        await response.EnsureSuccessWithErrorBodyAsync(ct);
    }
}
