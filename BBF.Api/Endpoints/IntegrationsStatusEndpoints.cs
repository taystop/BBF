using BBF.Api.Services;
using BBF.Shared.Contracts.Integrations;

namespace BBF.Api.Endpoints;

/// <summary>Backs Home.razor's dashboard tiles — the rest of that page's counts are composed client-side from the chat/wiki/services endpoints, which already exist.</summary>
public static class IntegrationsStatusEndpoints
{
    public static void MapIntegrationsStatusEndpoints(this WebApplication app)
    {
        app.MapGet("/api/integrations/ollama/status", async (OllamaService ollama, CancellationToken ct) =>
        {
            var online = await ollama.IsHealthyAsync();
            var models = online ? await ollama.GetModelsAsync() : [];
            return Results.Ok(new OllamaStatusDto(online, models));
        }).RequireAuthorization();
    }
}
