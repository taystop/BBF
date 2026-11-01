using System.Net.Http.Json;
using BBF.Shared.Contracts.Recurring;

namespace BBF.Shared.ApiClients;

public class HttpRecurringTransactionService(HttpClient http) : IRecurringTransactionService
{
    public async Task<List<RecurringTransactionDto>> GetActiveAsync(int groupId, CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<RecurringTransactionDto>>($"/api/groups/{groupId}/recurring-transactions", ct) ?? [];

    public async Task<RecurringTransactionDto> CreateAsync(int groupId, CreateRecurringTransactionRequest request, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync($"/api/groups/{groupId}/recurring-transactions", request, ct);
        await response.EnsureSuccessWithErrorBodyAsync(ct);
        return await response.Content.ReadFromJsonAsync<RecurringTransactionDto>(ct)
            ?? throw new InvalidOperationException("Empty response creating recurring transaction.");
    }

    public async Task UpdateAsync(int groupId, int ruleId, UpdateRecurringTransactionRequest request, CancellationToken ct = default)
    {
        var response = await http.PutAsJsonAsync($"/api/groups/{groupId}/recurring-transactions/{ruleId}", request, ct);
        await response.EnsureSuccessWithErrorBodyAsync(ct);
    }

    public async Task DeleteAsync(int groupId, int ruleId, CancellationToken ct = default)
    {
        var response = await http.DeleteAsync($"/api/groups/{groupId}/recurring-transactions/{ruleId}", ct);
        await response.EnsureSuccessWithErrorBodyAsync(ct);
    }
}
