using System.Net.Http.Json;
using BBF.Shared.Contracts.Emby;

namespace BBF.Api.Services;

public class EmbyService(HttpClient http, IConfiguration config)
{
    private readonly string _baseUrl = config["Emby:Url"] ?? "http://10.69.1.5:8096";
    private readonly string _apiKey = config["Emby:ApiKey"] ?? "";

    private string Url(string path) => $"{_baseUrl}{path}?api_key={_apiKey}";

    public async Task<EmbySystemInfo?> GetSystemInfoAsync(CancellationToken ct = default)
    {
        try
        {
            return await http.GetFromJsonAsync<EmbySystemInfo>(Url("/emby/System/Info"), ct);
        }
        catch { return null; }
    }

    public async Task<List<EmbyLibrary>> GetLibrariesAsync(CancellationToken ct = default)
    {
        try
        {
            // /Library/VirtualFolders returns an array directly
            var result = await http.GetFromJsonAsync<List<EmbyLibrary>>(Url("/emby/Library/VirtualFolders"), ct);
            return result ?? [];
        }
        catch { return []; }
    }

    public async Task<EmbyItemCounts?> GetItemCountsAsync(CancellationToken ct = default)
    {
        try
        {
            return await http.GetFromJsonAsync<EmbyItemCounts>(Url("/emby/Items/Counts"), ct);
        }
        catch { return null; }
    }

    public async Task<List<EmbySession>> GetActiveSessionsAsync(CancellationToken ct = default)
    {
        try
        {
            var sessions = await http.GetFromJsonAsync<List<EmbySession>>(Url("/emby/Sessions"), ct);
            return sessions?.Where(s => s.NowPlayingItem is not null).ToList() ?? [];
        }
        catch { return []; }
    }

    public async Task<List<EmbyItem>> GetRecentItemsAsync(int limit = 12, CancellationToken ct = default)
    {
        try
        {
            var url = $"{_baseUrl}/emby/Items/Latest?api_key={_apiKey}&Limit={limit}&Fields=Overview,DateCreated&EnableImages=true";
            var items = await http.GetFromJsonAsync<List<EmbyItem>>(url, ct) ?? [];
            foreach (var item in items.Where(i => i.HasPrimaryImage))
                item.ImageUrl = GetImageUrl(item.Id);
            return items;
        }
        catch { return []; }
    }

    private string GetImageUrl(string itemId, string imageType = "Primary", int? maxHeight = null)
    {
        var url = $"{_baseUrl}/emby/Items/{itemId}/Images/{imageType}?api_key={_apiKey}";
        if (maxHeight.HasValue)
            url += $"&maxHeight={maxHeight}";
        return url;
    }

    public async Task<bool> IsHealthyAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await http.GetAsync($"{_baseUrl}/emby/System/Ping?api_key={_apiKey}", ct);
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }
}
