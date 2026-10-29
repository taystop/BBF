using System.Net.Http.Json;
using BBF.Shared.Contracts.Budget;

namespace BBF.ApiClients;

public class HttpBudgetPeriodService(HttpClient http) : IBudgetPeriodService
{
    public async Task<BudgetPeriodDto?> GetPeriodAsync(int groupId, DateTime monthStart, CancellationToken ct = default)
    {
        var response = await http.GetAsync($"/api/groups/{groupId}/budget/periods?month={monthStart:yyyy-MM-dd}", ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        await response.EnsureSuccessWithErrorBodyAsync(ct);
        return await response.Content.ReadFromJsonAsync<BudgetPeriodDto>(ct);
    }

    public async Task<BudgetPeriodDto> GetOrCreateCurrentPeriodAsync(int groupId, CancellationToken ct = default)
    {
        var response = await http.GetAsync($"/api/groups/{groupId}/budget/periods", ct);
        await response.EnsureSuccessWithErrorBodyAsync(ct);
        return await response.Content.ReadFromJsonAsync<BudgetPeriodDto>(ct)
            ?? throw new InvalidOperationException("Empty response getting current budget period.");
    }

    public async Task UpsertLineItemAsync(int groupId, int periodId, UpsertLineItemRequest request, CancellationToken ct = default)
    {
        var response = await http.PutAsJsonAsync($"/api/groups/{groupId}/budget/periods/{periodId}/line-items", request, ct);
        await response.EnsureSuccessWithErrorBodyAsync(ct);
    }

    public async Task ClosePeriodAsync(int groupId, int periodId, CancellationToken ct = default)
    {
        var response = await http.PostAsync($"/api/groups/{groupId}/budget/periods/{periodId}/close", null, ct);
        await response.EnsureSuccessWithErrorBodyAsync(ct);
    }
}
