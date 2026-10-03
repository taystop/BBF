using System.Text.Json;
using BBF.Api.Services;
using BBF.Shared.Contracts.Chat;

namespace BBF.Api.Endpoints;

public static class ChatEndpoints
{
    public static void MapChatEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/chat").RequireAuthorization();

        group.MapGet("/conversations", async (ChatService chat, CancellationToken ct) =>
            Results.Ok(await chat.GetConversationsAsync(ct)));

        group.MapGet("/conversations/{conversationId:int}", async (int conversationId, ChatService chat, CancellationToken ct) =>
        {
            var convo = await chat.GetConversationAsync(conversationId, ct);
            return convo is null ? Results.NotFound() : Results.Ok(convo);
        });

        group.MapDelete("/conversations/{conversationId:int}", async (int conversationId, ChatService chat, CancellationToken ct) =>
        {
            await chat.DeleteConversationAsync(conversationId, ct);
            return Results.NoContent();
        });

        group.MapPut("/conversations/{conversationId:int}/shares", async (int conversationId, UpdateChatSharesRequest request, ChatService chat, CancellationToken ct) =>
        {
            await chat.UpdateSharesAsync(conversationId, request, ct);
            return Results.NoContent();
        });

        group.MapGet("/models", async (ChatService chat, CancellationToken ct) =>
            Results.Ok(await chat.GetAvailableModelsAsync(ct)));

        group.MapPost("/messages/{messageId:int}/save-to-wiki", async (int messageId, ChatService chat, CancellationToken ct) =>
        {
            try
            {
                var title = await chat.SaveMessageToWikiAsync(messageId, ct);
                return Results.Ok(new { title });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
        });

        // Real SSE stream (not a buffered JSON array) so the client gets tokens as they're generated —
        // mirrors how OllamaService itself consumes Ollama's own streaming endpoint internally.
        group.MapPost("/stream", async (int? conversationId, SendChatMessageRequest request, ChatService chat, HttpResponse response, CancellationToken ct) =>
        {
            response.ContentType = "text/event-stream";
            response.Headers.CacheControl = "no-cache";
            response.Headers["X-Accel-Buffering"] = "no";

            await foreach (var evt in chat.SendMessageStreamAsync(conversationId, request, ct))
            {
                var json = JsonSerializer.Serialize(evt);
                await response.WriteAsync($"data: {json}\n\n", ct);
                await response.Body.FlushAsync(ct);
            }
        });
    }
}
