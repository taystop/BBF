using System.Net.Http.Json;
using BBF.Shared.Contracts.Planning;

namespace BBF.Shared.ApiClients;

public class HttpPlanningService(HttpClient http) : IPlanningService
{
    public async Task<MonthPlanDto> GetMonthPlanAsync(int groupId, DateTime month, CancellationToken ct = default) =>
        await http.GetFromJsonAsync<MonthPlanDto>($"/api/groups/{groupId}/planning/month?month={month:yyyy-MM-01}", ct)
            ?? throw new InvalidOperationException("Empty month plan response.");

    public async Task<MonthReviewDto> GetMonthReviewAsync(int groupId, DateTime month, CancellationToken ct = default) =>
        await http.GetFromJsonAsync<MonthReviewDto>($"/api/groups/{groupId}/planning/review?month={month:yyyy-MM-01}", ct)
            ?? throw new InvalidOperationException("Empty month review response.");
}
