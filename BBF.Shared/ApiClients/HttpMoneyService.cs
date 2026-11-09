using System.Net.Http.Json;
using BBF.Shared.Contracts.Money;
using BBF.Shared.Contracts.Transactions;

namespace BBF.Shared.ApiClients;

public class HttpMoneyService(HttpClient http) : IMoneyService
{
    public async Task<MoneySummaryDto> GetSummaryAsync(int groupId, CancellationToken ct = default) =>
        await http.GetFromJsonAsync<MoneySummaryDto>($"/api/groups/{groupId}/money/summary", ct)
            ?? throw new InvalidOperationException("Empty money summary response.");

    public async Task<ForecastDto> GetForecastAsync(int groupId, int days, CancellationToken ct = default) =>
        await http.GetFromJsonAsync<ForecastDto>($"/api/groups/{groupId}/money/forecast?days={days}", ct)
            ?? throw new InvalidOperationException("Empty forecast response.");

    public async Task<List<TransactionDto>> GetToReviewAsync(int groupId, CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<TransactionDto>>($"/api/groups/{groupId}/money/review", ct) ?? [];

    public async Task<CategorizeResult> CategorizeAsync(int groupId, int transactionId, CategorizeRequest request, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync($"/api/groups/{groupId}/money/review/{transactionId}/categorize", request, ct);
        await response.EnsureSuccessWithErrorBodyAsync(ct);
        return await response.Content.ReadFromJsonAsync<CategorizeResult>(ct) ?? new CategorizeResult(0, null);
    }

    public async Task<List<MerchantRuleDto>> GetMerchantRulesAsync(int groupId, CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<MerchantRuleDto>>($"/api/groups/{groupId}/money/merchant-rules", ct) ?? [];

    public async Task DeleteMerchantRuleAsync(int groupId, int ruleId, CancellationToken ct = default)
    {
        var response = await http.DeleteAsync($"/api/groups/{groupId}/money/merchant-rules/{ruleId}", ct);
        await response.EnsureSuccessWithErrorBodyAsync(ct);
    }

    public async Task<List<RecurringSuggestionDto>> GetSuggestionsAsync(int groupId, CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<RecurringSuggestionDto>>($"/api/groups/{groupId}/money/suggestions", ct) ?? [];

    public async Task DismissSuggestionAsync(int groupId, DismissSuggestionRequest request, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync($"/api/groups/{groupId}/money/suggestions/dismiss", request, ct);
        await response.EnsureSuccessWithErrorBodyAsync(ct);
    }
}
