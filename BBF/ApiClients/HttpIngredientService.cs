using System.Net.Http.Json;
using BBF.Shared.Contracts.MealPlanning;

namespace BBF.ApiClients;

public class HttpIngredientService(HttpClient http) : IIngredientService
{
    public async Task<List<IngredientDto>> GetIngredientsAsync(int groupId, CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<IngredientDto>>($"/api/groups/{groupId}/ingredients", ct) ?? [];

    public async Task<IngredientDto> CreateAsync(int groupId, CreateIngredientRequest request, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync($"/api/groups/{groupId}/ingredients", request, ct);
        await response.EnsureSuccessWithErrorBodyAsync(ct);
        return await response.Content.ReadFromJsonAsync<IngredientDto>(ct)
            ?? throw new InvalidOperationException("Empty response creating ingredient.");
    }

    public async Task UpdateAsync(int groupId, int ingredientId, UpdateIngredientRequest request, CancellationToken ct = default)
    {
        var response = await http.PutAsJsonAsync($"/api/groups/{groupId}/ingredients/{ingredientId}", request, ct);
        await response.EnsureSuccessWithErrorBodyAsync(ct);
    }

    public async Task DeleteAsync(int groupId, int ingredientId, CancellationToken ct = default)
    {
        var response = await http.DeleteAsync($"/api/groups/{groupId}/ingredients/{ingredientId}", ct);
        await response.EnsureSuccessWithErrorBodyAsync(ct);
    }
}
