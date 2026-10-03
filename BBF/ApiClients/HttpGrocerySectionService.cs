using System.Net.Http.Json;
using BBF.Shared.Contracts.MealPlanning;

namespace BBF.ApiClients;

public class HttpGrocerySectionService(HttpClient http) : IGrocerySectionService
{
    public async Task<List<GrocerySectionDto>> GetSectionsAsync(int groupId, CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<GrocerySectionDto>>($"/api/groups/{groupId}/grocery-sections", ct) ?? [];

    public async Task<GrocerySectionDto> CreateAsync(int groupId, CreateGrocerySectionRequest request, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync($"/api/groups/{groupId}/grocery-sections", request, ct);
        await response.EnsureSuccessWithErrorBodyAsync(ct);
        return await response.Content.ReadFromJsonAsync<GrocerySectionDto>(ct)
            ?? throw new InvalidOperationException("Empty response creating grocery section.");
    }

    public async Task UpdateAsync(int groupId, int sectionId, UpdateGrocerySectionRequest request, CancellationToken ct = default)
    {
        var response = await http.PutAsJsonAsync($"/api/groups/{groupId}/grocery-sections/{sectionId}", request, ct);
        await response.EnsureSuccessWithErrorBodyAsync(ct);
    }

    public async Task DeleteAsync(int groupId, int sectionId, CancellationToken ct = default)
    {
        var response = await http.DeleteAsync($"/api/groups/{groupId}/grocery-sections/{sectionId}", ct);
        await response.EnsureSuccessWithErrorBodyAsync(ct);
    }

    public async Task ReorderAsync(int groupId, ReorderGrocerySectionsRequest request, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync($"/api/groups/{groupId}/grocery-sections/reorder", request, ct);
        await response.EnsureSuccessWithErrorBodyAsync(ct);
    }
}
