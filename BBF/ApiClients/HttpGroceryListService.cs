using System.Net.Http.Json;
using BBF.Shared.Contracts.MealPlanning;

namespace BBF.ApiClients;

public class HttpGroceryListService(HttpClient http) : IGroceryListService
{
    public async Task<GroceryListDto?> GetAsync(int groupId, int weekId, CancellationToken ct = default)
    {
        var response = await http.GetAsync($"/api/groups/{groupId}/meal-plan/weeks/{weekId}/grocery-list", ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        await response.EnsureSuccessWithErrorBodyAsync(ct);
        return await response.Content.ReadFromJsonAsync<GroceryListDto>(ct);
    }

    public async Task<GroceryListDto?> GenerateAsync(int groupId, int weekId, CancellationToken ct = default)
    {
        var response = await http.PostAsync($"/api/groups/{groupId}/meal-plan/weeks/{weekId}/grocery-list/generate", null, ct);
        await response.EnsureSuccessWithErrorBodyAsync(ct);
        return await response.Content.ReadFromJsonAsync<GroceryListDto>(ct);
    }

    public async Task AddManualItemAsync(int groupId, int groceryListId, AddManualGroceryItemRequest request, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync($"/api/groups/{groupId}/grocery-lists/{groceryListId}/items", request, ct);
        await response.EnsureSuccessWithErrorBodyAsync(ct);
    }

    public async Task RemoveItemAsync(int groupId, int itemId, CancellationToken ct = default)
    {
        var response = await http.DeleteAsync($"/api/groups/{groupId}/grocery-list-items/{itemId}", ct);
        await response.EnsureSuccessWithErrorBodyAsync(ct);
    }

    public async Task SetCheckedAsync(int groupId, int itemId, bool isChecked, CancellationToken ct = default)
    {
        var response = await http.PostAsync($"/api/groups/{groupId}/grocery-list-items/{itemId}/checked?isChecked={isChecked}", null, ct);
        await response.EnsureSuccessWithErrorBodyAsync(ct);
    }
}
