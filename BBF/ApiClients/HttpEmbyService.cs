using System.Net.Http.Json;
using BBF.Shared.Contracts.Emby;

namespace BBF.ApiClients;

public class HttpEmbyService(HttpClient http) : IEmbyService
{
    public async Task<EmbySystemInfo?> GetSystemInfoAsync(CancellationToken ct = default)
    {
        var response = await http.GetAsync("/api/integrations/emby/system-info", ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        await response.EnsureSuccessWithErrorBodyAsync(ct);
        return await response.Content.ReadFromJsonAsync<EmbySystemInfo>(ct);
    }

    public async Task<List<EmbyLibrary>> GetLibrariesAsync(CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<EmbyLibrary>>("/api/integrations/emby/libraries", ct) ?? [];

    public async Task<EmbyItemCounts?> GetItemCountsAsync(CancellationToken ct = default)
    {
        var response = await http.GetAsync("/api/integrations/emby/item-counts", ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        await response.EnsureSuccessWithErrorBodyAsync(ct);
        return await response.Content.ReadFromJsonAsync<EmbyItemCounts>(ct);
    }

    public async Task<List<EmbySession>> GetActiveSessionsAsync(CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<EmbySession>>("/api/integrations/emby/sessions", ct) ?? [];

    public async Task<List<EmbyItem>> GetRecentItemsAsync(int limit = 12, CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<EmbyItem>>($"/api/integrations/emby/recent-items?limit={limit}", ct) ?? [];

    public async Task<bool> IsHealthyAsync(CancellationToken ct = default)
    {
        try
        {
            return await http.GetFromJsonAsync<bool>("/api/integrations/emby/health", ct);
        }
        catch
        {
            return false;
        }
    }
}
