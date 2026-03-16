using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BBF.Services;

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
            var result = await http.GetFromJsonAsync<EmbyVirtualFolders>(Url("/emby/Library/VirtualFolders"), ct);
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
            return await http.GetFromJsonAsync<List<EmbyItem>>(url, ct) ?? [];
        }
        catch { return []; }
    }

    public string GetImageUrl(string itemId, string imageType = "Primary", int? maxHeight = null)
    {
        var url = $"{_baseUrl}/emby/Items/{itemId}/Images/{imageType}?api_key={_apiKey}";
        if (maxHeight.HasValue)
            url += $"&maxHeight={maxHeight}";
        return url;
    }

    public async Task<bool> IsHealthyAsync()
    {
        try
        {
            var response = await http.GetAsync($"{_baseUrl}/emby/System/Ping?api_key={_apiKey}");
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }
}

// --- DTOs ---

public class EmbySystemInfo
{
    [JsonPropertyName("ServerName")]
    public string ServerName { get; set; } = "";

    [JsonPropertyName("Version")]
    public string Version { get; set; } = "";

    [JsonPropertyName("OperatingSystem")]
    public string OperatingSystem { get; set; } = "";
}

public class EmbyItemCounts
{
    [JsonPropertyName("MovieCount")]
    public int MovieCount { get; set; }

    [JsonPropertyName("SeriesCount")]
    public int SeriesCount { get; set; }

    [JsonPropertyName("EpisodeCount")]
    public int EpisodeCount { get; set; }

    [JsonPropertyName("ArtistCount")]
    public int ArtistCount { get; set; }

    [JsonPropertyName("SongCount")]
    public int SongCount { get; set; }

    [JsonPropertyName("AlbumCount")]
    public int AlbumCount { get; set; }

    [JsonPropertyName("BookCount")]
    public int BookCount { get; set; }
}

public class EmbySession
{
    [JsonPropertyName("Id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("UserName")]
    public string UserName { get; set; } = "";

    [JsonPropertyName("Client")]
    public string Client { get; set; } = "";

    [JsonPropertyName("DeviceName")]
    public string DeviceName { get; set; } = "";

    [JsonPropertyName("NowPlayingItem")]
    public EmbyNowPlaying? NowPlayingItem { get; set; }

    [JsonPropertyName("PlayState")]
    public EmbyPlayState? PlayState { get; set; }
}

public class EmbyNowPlaying
{
    [JsonPropertyName("Name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("Id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("Type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("SeriesName")]
    public string? SeriesName { get; set; }

    [JsonPropertyName("SeasonName")]
    public string? SeasonName { get; set; }

    [JsonPropertyName("RunTimeTicks")]
    public long? RunTimeTicks { get; set; }
}

public class EmbyPlayState
{
    [JsonPropertyName("PositionTicks")]
    public long? PositionTicks { get; set; }

    [JsonPropertyName("IsPaused")]
    public bool IsPaused { get; set; }

    [JsonPropertyName("IsMuted")]
    public bool IsMuted { get; set; }
}

public class EmbyItem
{
    [JsonPropertyName("Name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("Id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("Type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("SeriesName")]
    public string? SeriesName { get; set; }

    [JsonPropertyName("Overview")]
    public string? Overview { get; set; }

    [JsonPropertyName("DateCreated")]
    public DateTime? DateCreated { get; set; }

    [JsonPropertyName("HasPrimaryImage")]
    public bool HasPrimaryImage { get; set; }

    [JsonPropertyName("ImageTags")]
    public JsonElement? ImageTags { get; set; }
}

public class EmbyLibrary
{
    [JsonPropertyName("Name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("CollectionType")]
    public string? CollectionType { get; set; }

    [JsonPropertyName("ItemId")]
    public string ItemId { get; set; } = "";
}

// Deserialize helper — /Library/VirtualFolders returns an array directly
public class EmbyVirtualFolders : List<EmbyLibrary>;
