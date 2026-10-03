using System.Text.Json;
using System.Text.Json.Serialization;

namespace BBF.Shared.Contracts.Emby;

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

    /// <summary>
    /// Pre-resolved, API-key-included image URL set by BBF.Api — unlike the original (which built this
    /// client-side with direct config access), Web/Mobile clients never see the Emby API key directly,
    /// so the server bakes the full URL in here instead of exposing a URL-builder method.
    /// </summary>
    public string? ImageUrl { get; set; }
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
