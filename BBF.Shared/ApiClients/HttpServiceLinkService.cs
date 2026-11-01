using System.Net.Http.Json;
using BBF.Shared.Contracts.Services;

namespace BBF.Shared.ApiClients;

public class HttpServiceLinkService(HttpClient http) : IServiceLinkService
{
    public async Task<List<ServiceLinkDto>> GetAllAsync(CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<ServiceLinkDto>>("/api/services", ct) ?? [];

    public async Task<List<ServiceLinkDto>> GetMonitoredAsync(CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<ServiceLinkDto>>("/api/services/monitored", ct) ?? [];

    public async Task<List<RecentHealthLogDto>> GetRecentHealthLogsAsync(int take = 20, CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<RecentHealthLogDto>>($"/api/services/health-logs/recent?take={take}", ct) ?? [];

    public async Task<ServiceLinkDto> SaveAsync(UpsertServiceLinkRequest request, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync("/api/services", request, ct);
        await response.EnsureSuccessWithErrorBodyAsync(ct);
        return await response.Content.ReadFromJsonAsync<ServiceLinkDto>(ct)
            ?? throw new InvalidOperationException("Empty response saving service link.");
    }

    public async Task DeleteAsync(int serviceLinkId, CancellationToken ct = default)
    {
        var response = await http.DeleteAsync($"/api/services/{serviceLinkId}", ct);
        await response.EnsureSuccessWithErrorBodyAsync(ct);
    }

    public async Task<ServiceHealthLogDto> CheckHealthAsync(int serviceLinkId, CancellationToken ct = default)
    {
        var response = await http.PostAsync($"/api/services/{serviceLinkId}/check", null, ct);
        await response.EnsureSuccessWithErrorBodyAsync(ct);
        return await response.Content.ReadFromJsonAsync<ServiceHealthLogDto>(ct)
            ?? throw new InvalidOperationException("Empty response checking service health.");
    }
}
