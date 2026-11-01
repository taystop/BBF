using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using BBF.Shared.Contracts.Chat;

namespace BBF.Shared.ApiClients;

public class HttpChatService(HttpClient http) : IChatService
{
    public async Task<List<ChatConversationSummaryDto>> GetConversationsAsync(CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<ChatConversationSummaryDto>>("/api/chat/conversations", ct) ?? [];

    public async Task<ChatConversationDetailDto?> GetConversationAsync(int conversationId, CancellationToken ct = default)
    {
        var response = await http.GetAsync($"/api/chat/conversations/{conversationId}", ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        await response.EnsureSuccessWithErrorBodyAsync(ct);
        return await response.Content.ReadFromJsonAsync<ChatConversationDetailDto>(ct);
    }

    public async Task DeleteConversationAsync(int conversationId, CancellationToken ct = default)
    {
        var response = await http.DeleteAsync($"/api/chat/conversations/{conversationId}", ct);
        await response.EnsureSuccessWithErrorBodyAsync(ct);
    }

    public async Task UpdateSharesAsync(int conversationId, UpdateChatSharesRequest request, CancellationToken ct = default)
    {
        var response = await http.PutAsJsonAsync($"/api/chat/conversations/{conversationId}/shares", request, ct);
        await response.EnsureSuccessWithErrorBodyAsync(ct);
    }

    public async Task<List<string>> GetAvailableModelsAsync(CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<string>>("/api/chat/models", ct) ?? [];

    public async Task<string> SaveMessageToWikiAsync(int messageId, CancellationToken ct = default)
    {
        var response = await http.PostAsync($"/api/chat/messages/{messageId}/save-to-wiki", null, ct);
        await response.EnsureSuccessWithErrorBodyAsync(ct);
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>(ct);
        return body?.GetValueOrDefault("title") ?? throw new InvalidOperationException("Empty response saving to wiki.");
    }

    public async IAsyncEnumerable<ChatStreamEvent> SendMessageStreamAsync(
        int? conversationId, SendChatMessageRequest request, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var url = conversationId is int id ? $"/api/chat/stream?conversationId={id}" : "/api/chat/stream";
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(request)
        };

        using var response = await http.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream && !ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (string.IsNullOrEmpty(line) || !line.StartsWith("data: ", StringComparison.Ordinal)) continue;

            var json = line["data: ".Length..];
            var evt = JsonSerializer.Deserialize<ChatStreamEvent>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (evt is not null)
                yield return evt;
        }
    }
}
