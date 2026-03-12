using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BBF.Services;

public record ChatStreamChunk(string Text, bool IsThinking);

public class OllamaService(HttpClient http, IConfiguration config)
{
    private readonly string _baseUrl = config["Ollama:BaseUrl"] ?? "http://10.69.1.5:11434";
    private readonly string _defaultModel = config["Ollama:DefaultModel"] ?? "qwen3.5:9b";

    private const string SystemPrompt =
        "You are BBF, a knowledgeable and helpful AI assistant. You can answer questions on any topic. When you are unsure or don't have reliable information, say so honestly rather than guessing. Give clear, well-structured answers. Use bullet points or numbered steps when helpful. Be concise but thorough.";

    public async IAsyncEnumerable<ChatStreamChunk> ChatStreamAsync(
        List<OllamaChatMessage> messages,
        string? model = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var fullMessages = PrependSystemPrompt(messages);

        var request = new
        {
            model = model ?? _defaultModel,
            messages = fullMessages,
            stream = true,
            think = false,
            options = new
            {
                num_ctx = 8192,
                repeat_penalty = 1.2,
                temperature = 0.7,
                top_p = 0.8,
                top_k = 20
            }
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
            if (chunk?.Message is null) continue;

            // Thinking content comes via the "thinking" field in the message
            if (!string.IsNullOrEmpty(chunk.Message.Thinking))
                yield return new ChatStreamChunk(chunk.Message.Thinking, IsThinking: true);

            if (!string.IsNullOrEmpty(chunk.Message.Content))
                yield return new ChatStreamChunk(chunk.Message.Content, IsThinking: false);
        }
    }

    public async Task<string> ChatAsync(List<OllamaChatMessage> messages, string? model = null, CancellationToken ct = default)
    {
        var fullMessages = PrependSystemPrompt(messages);

        var request = new
        {
            model = model ?? _defaultModel,
            messages = fullMessages,
            stream = false,
            think = false,
            options = new
            {
                num_ctx = 8192,
                repeat_penalty = 1.2,
                temperature = 0.7,
                top_p = 0.8,
                top_k = 20
            }
        };

        var response = await http.PostAsJsonAsync($"{_baseUrl}/api/chat", request, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OllamaChatResponse>(ct);
        return result?.Message?.Content ?? string.Empty;
    }

    private static List<OllamaChatMessage> PrependSystemPrompt(List<OllamaChatMessage> messages)
    {
        var result = new List<OllamaChatMessage>(messages.Count + 1);
        // Only add system prompt if there isn't one already
        if (messages.Count == 0 || messages[0].Role != "system")
        {
            result.Add(new OllamaChatMessage { Role = "system", Content = SystemPrompt });
        }
        result.AddRange(messages);
        return result;
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

    [JsonPropertyName("thinking")]
    public string? Thinking { get; set; }
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
