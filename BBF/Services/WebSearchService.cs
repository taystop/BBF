using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Web;

namespace BBF.Services;

public class WebSearchService(HttpClient http, IConfiguration config)
{
    private readonly string _baseUrl = config["SearXNG:BaseUrl"] ?? "http://10.69.1.5:8888";

    public async Task<List<SearchResult>> SearchAsync(string query, int maxResults = 5, CancellationToken ct = default)
    {
        try
        {
            var encoded = HttpUtility.UrlEncode(query);
            var url = $"{_baseUrl}/search?q={encoded}&format=json&categories=general&language=en";

            var response = await http.GetFromJsonAsync<SearXNGResponse>(url, ct);

            return response?.Results?
                .Take(maxResults)
                .Select(r => new SearchResult
                {
                    Title = r.Title ?? string.Empty,
                    Url = r.Url ?? string.Empty,
                    Content = r.Content ?? string.Empty
                })
                .ToList() ?? [];
        }
        catch
        {
            return [];
        }
    }

    public async Task<bool> IsHealthyAsync()
    {
        try
        {
            var response = await http.GetAsync($"{_baseUrl}/healthz");
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public static string FormatResultsAsContext(List<SearchResult> results)
    {
        if (results.Count == 0) return string.Empty;

        var lines = new List<string> { "Here are relevant web search results to help answer the question:" };

        for (var i = 0; i < results.Count; i++)
        {
            var r = results[i];
            lines.Add($"\n[{i + 1}] {r.Title}");
            lines.Add($"    URL: {r.Url}");
            if (!string.IsNullOrWhiteSpace(r.Content))
                lines.Add($"    {r.Content}");
        }

        lines.Add("\nUse the above search results to inform your answer. Cite sources when relevant by mentioning the title or URL. If the search results don't contain relevant information, answer from your own knowledge and say so.");

        return string.Join("\n", lines);
    }
}

public class SearchResult
{
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

public class SearXNGResponse
{
    [JsonPropertyName("results")]
    public List<SearXNGResult>? Results { get; set; }
}

public class SearXNGResult
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("content")]
    public string? Content { get; set; }
}
