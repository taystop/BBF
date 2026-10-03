using BBF.Api.Services;
using BBF.Shared.Contracts.HomeAssistant;

namespace BBF.Api.Endpoints;

public static class HomeAssistantEndpoints
{
    public static void MapHomeAssistantEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/integrations/home-assistant").RequireAuthorization();

        group.MapGet("/entities", async (string? domain, HomeAssistantService ha, CancellationToken ct) =>
            Results.Ok(domain is null ? await ha.GetEntitiesAsync(ct) : await ha.GetEntitiesByDomainAsync(domain, ct)));

        group.MapGet("/entities/{entityId}", async (string entityId, HomeAssistantService ha, CancellationToken ct) =>
        {
            var entity = await ha.GetEntityAsync(entityId, ct);
            return entity is null ? Results.NotFound() : Results.Ok(entity);
        });

        group.MapPost("/call-service", async (HaServiceCallRequest request, HomeAssistantService ha, CancellationToken ct) =>
            Results.Ok(await ha.CallServiceAsync(request, ct)));

        group.MapPost("/entities/{entityId}/turn-on", async (string entityId, HomeAssistantService ha, CancellationToken ct) =>
            Results.Ok(await ha.TurnOnAsync(entityId, ct)));

        group.MapPost("/entities/{entityId}/turn-off", async (string entityId, HomeAssistantService ha, CancellationToken ct) =>
            Results.Ok(await ha.TurnOffAsync(entityId, ct)));

        group.MapPost("/entities/{entityId}/toggle", async (string entityId, HomeAssistantService ha, CancellationToken ct) =>
            Results.Ok(await ha.ToggleAsync(entityId, ct)));

        group.MapPost("/entities/{entityId}/temperature", async (string entityId, double value, HomeAssistantService ha, CancellationToken ct) =>
            Results.Ok(await ha.SetTemperatureAsync(entityId, value, ct)));

        group.MapPost("/entities/{entityId}/hvac-mode", async (string entityId, string mode, HomeAssistantService ha, CancellationToken ct) =>
            Results.Ok(await ha.SetHvacModeAsync(entityId, mode, ct)));

        group.MapPost("/entities/{entityId}/fan-speed", async (string entityId, string percentage, HomeAssistantService ha, CancellationToken ct) =>
            Results.Ok(await ha.SetFanSpeedAsync(entityId, percentage, ct)));

        group.MapGet("/health", async (HomeAssistantService ha, CancellationToken ct) =>
            Results.Ok(await ha.IsHealthyAsync(ct)));
    }
}
