using System.Net.Http.Json;
using BBF.Shared.Contracts.Budget;

namespace BBF.ApiClients;

public class HttpBudgetCategoryService(HttpClient http) : IBudgetCategoryService
{
    public async Task<List<BudgetCategoryDto>> GetCategoriesAsync(int groupId, CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<BudgetCategoryDto>>($"/api/groups/{groupId}/budget/categories", ct) ?? [];

    public async Task<BudgetCategoryDto> CreateAsync(int groupId, UpsertBudgetCategoryRequest request, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync($"/api/groups/{groupId}/budget/categories", request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<BudgetCategoryDto>(ct)
            ?? throw new InvalidOperationException("Empty response creating category.");
    }

    public async Task UpdateAsync(int groupId, int categoryId, UpsertBudgetCategoryRequest request, CancellationToken ct = default)
    {
        var response = await http.PutAsJsonAsync($"/api/groups/{groupId}/budget/categories/{categoryId}", request, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task<DeleteBudgetCategoryResult> DeleteAsync(int groupId, int categoryId, CancellationToken ct = default)
    {
        var response = await http.DeleteAsync($"/api/groups/{groupId}/budget/categories/{categoryId}", ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DeleteBudgetCategoryResult>(ct)
            ?? throw new InvalidOperationException("Empty response deleting category.");
    }
}
