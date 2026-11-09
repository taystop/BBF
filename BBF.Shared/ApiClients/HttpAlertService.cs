using System.Net.Http.Json;
using BBF.Shared.Contracts.Alerts;

namespace BBF.Shared.ApiClients;

public class HttpAlertService(HttpClient http) : IAlertService
{
    public async Task<List<FinanceAlertDto>> GetAlertsAsync(int groupId, CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<FinanceAlertDto>>($"/api/groups/{groupId}/alerts", ct) ?? [];

    public async Task DismissAsync(int groupId, int alertId, CancellationToken ct = default)
    {
        var response = await http.PostAsync($"/api/groups/{groupId}/alerts/{alertId}/dismiss", null, ct);
        await response.EnsureSuccessWithErrorBodyAsync(ct);
    }

    public async Task DismissAllAsync(int groupId, CancellationToken ct = default)
    {
        var response = await http.PostAsync($"/api/groups/{groupId}/alerts/dismiss-all", null, ct);
        await response.EnsureSuccessWithErrorBodyAsync(ct);
    }

    public async Task<AlertPreferencesDto> GetPreferencesAsync(int groupId, CancellationToken ct = default) =>
        await http.GetFromJsonAsync<AlertPreferencesDto>($"/api/groups/{groupId}/alerts/preferences", ct) ?? new AlertPreferencesDto();

    public async Task UpdatePreferencesAsync(int groupId, AlertPreferencesDto preferences, CancellationToken ct = default)
    {
        var response = await http.PutAsJsonAsync($"/api/groups/{groupId}/alerts/preferences", preferences, ct);
        await response.EnsureSuccessWithErrorBodyAsync(ct);
    }

    public async Task DecideLeftoverAsync(int groupId, int decisionId, LeftoverDecisionRequest request, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync($"/api/groups/{groupId}/leftover/{decisionId}/decision", request, ct);
        await response.EnsureSuccessWithErrorBodyAsync(ct);
    }
}
