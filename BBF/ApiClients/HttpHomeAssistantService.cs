using System.Net.Http.Json;
using System.Web;
using BBF.Shared.Contracts.HomeAssistant;

namespace BBF.ApiClients;

public class HttpHomeAssistantService(HttpClient http) : IHomeAssistantService
{
    public async Task<List<HaEntity>> GetEntitiesAsync(CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<HaEntity>>("/api/integrations/home-assistant/entities", ct) ?? [];

    public async Task<List<HaEntity>> GetEntitiesByDomainAsync(string domain, CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<HaEntity>>($"/api/integrations/home-assistant/entities?domain={HttpUtility.UrlEncode(domain)}", ct) ?? [];

    public async Task<HaEntity?> GetEntityAsync(string entityId, CancellationToken ct = default)
    {
        var response = await http.GetAsync($"/api/integrations/home-assistant/entities/{entityId}", ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        await response.EnsureSuccessWithErrorBodyAsync(ct);
        return await response.Content.ReadFromJsonAsync<HaEntity>(ct);
    }

    public async Task<bool> CallServiceAsync(HaServiceCallRequest request, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync("/api/integrations/home-assistant/call-service", request, ct);
        return response.IsSuccessStatusCode && await response.Content.ReadFromJsonAsync<bool>(ct);
    }

    public async Task<bool> TurnOnAsync(string entityId, CancellationToken ct = default) =>
        await PostForBoolAsync($"/api/integrations/home-assistant/entities/{entityId}/turn-on", ct);

    public async Task<bool> TurnOffAsync(string entityId, CancellationToken ct = default) =>
        await PostForBoolAsync($"/api/integrations/home-assistant/entities/{entityId}/turn-off", ct);

    public async Task<bool> ToggleAsync(string entityId, CancellationToken ct = default) =>
        await PostForBoolAsync($"/api/integrations/home-assistant/entities/{entityId}/toggle", ct);

    public async Task<bool> SetTemperatureAsync(string entityId, double temperature, CancellationToken ct = default) =>
        await PostForBoolAsync($"/api/integrations/home-assistant/entities/{entityId}/temperature?value={temperature}", ct);

    public async Task<bool> SetHvacModeAsync(string entityId, string mode, CancellationToken ct = default) =>
        await PostForBoolAsync($"/api/integrations/home-assistant/entities/{entityId}/hvac-mode?mode={HttpUtility.UrlEncode(mode)}", ct);

    public async Task<bool> SetFanSpeedAsync(string entityId, string percentage, CancellationToken ct = default) =>
        await PostForBoolAsync($"/api/integrations/home-assistant/entities/{entityId}/fan-speed?percentage={HttpUtility.UrlEncode(percentage)}", ct);

    public async Task<bool> IsHealthyAsync(CancellationToken ct = default)
    {
        try
        {
            return await http.GetFromJsonAsync<bool>("/api/integrations/home-assistant/health", ct);
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> PostForBoolAsync(string url, CancellationToken ct)
    {
        var response = await http.PostAsync(url, null, ct);
        return response.IsSuccessStatusCode && await response.Content.ReadFromJsonAsync<bool>(ct);
    }
}
