using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BBF.Services;

public class OllamaService(HttpClient http, IConfiguration config)
{
    private readonly string _baseUrl = config["Ollama:BaseUrl"] ?? "http://10.69.1.5:11434";
    private readonly string _defaultModel = config["Ollama:DefaultModel"] ?? "qwen3.5:9b";

    public async IAsyncEnumerable<string> ChatStreamAsync(
        List<OllamaChatMessage> messages,
        string? model = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var request = new
        {
            model = model ?? _defaultModel,
            messages,
            stream = true
        };

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/api/chat")
        {
            Content = JsonContent.Create(request)
        };

        using var response = await http.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        string? line;
        while ((line = await reader.ReadLineAsync(ct)) is not null && !ct.IsCancellationRequested)
        {
            if (string.IsNullOrEmpty(line)) continue;

            var chunk = JsonSerializer.Deserialize<OllamaChatResponse>(line);
            if (chunk?.Message?.Content is not null)
                yield return chunk.Message.Content;
        }
    }

    public async Task<string> ChatAsync(List<OllamaChatMessage> messages, string? model = null, CancellationToken ct = default)
    {
        var request = new
        {
            model = model ?? _defaultModel,
            messages,
            stream = false
        };

        var response = await http.PostAsJsonAsync($"{_baseUrl}/api/chat", request, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OllamaChatResponse>(ct);
        return result?.Message?.Content ?? string.Empty;
    }

    public async Task<bool> IsHealthyAsync()
    {
        try
        {
            var response = await http.GetAsync($"{_baseUrl}/api/tags");
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<List<string>> GetModelsAsync()
    {
        try
        {
            var response = await http.GetFromJsonAsync<OllamaTagsResponse>($"{_baseUrl}/api/tags");
            return response?.Models?.Select(m => m.Name).ToList() ?? [];
        }
        catch { return []; }
    }
}

public class OllamaChatMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
}

public class OllamaChatResponse
{
    [JsonPropertyName("message")]
    public OllamaChatMessage? Message { get; set; }

    [JsonPropertyName("done")]
    public bool Done { get; set; }
}

public class OllamaTagsResponse
{
    [JsonPropertyName("models")]
    public List<OllamaModel>? Models { get; set; }
}

public class OllamaModel
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}
