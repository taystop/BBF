using System.Net.Http.Json;
using BBF.Shared.Contracts.Amp;

namespace BBF.Shared.ApiClients;

public class HttpAmpService(HttpClient http) : IAmpService
{
    public async Task<List<AmpInstance>> GetInstancesAsync(CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<AmpInstance>>("/api/integrations/amp/instances", ct) ?? [];

    public async Task<AmpInstanceStatus?> GetInstanceStatusAsync(string instanceId, CancellationToken ct = default)
    {
        var response = await http.GetAsync($"/api/integrations/amp/instances/{instanceId}/status", ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        await response.EnsureSuccessWithErrorBodyAsync(ct);
        return await response.Content.ReadFromJsonAsync<AmpInstanceStatus>(ct);
    }

    public async Task<bool> StartInstanceAsync(string instanceId, CancellationToken ct = default) =>
        await PostForBoolAsync($"/api/integrations/amp/instances/{instanceId}/start", ct);

    public async Task<bool> StopInstanceAsync(string instanceId, CancellationToken ct = default) =>
        await PostForBoolAsync($"/api/integrations/amp/instances/{instanceId}/stop", ct);

    public async Task<bool> RestartInstanceAsync(string instanceId, CancellationToken ct = default) =>
        await PostForBoolAsync($"/api/integrations/amp/instances/{instanceId}/restart", ct);

    public async Task<bool> IsHealthyAsync(CancellationToken ct = default)
    {
        try
        {
            return await http.GetFromJsonAsync<bool>("/api/integrations/amp/health", ct);
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
