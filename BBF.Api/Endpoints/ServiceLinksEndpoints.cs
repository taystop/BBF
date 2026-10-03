using BBF.Api.Services;
using BBF.Shared.Contracts.Services;

namespace BBF.Api.Endpoints;

public static class ServiceLinksEndpoints
{
    public static void MapServiceLinksEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/services").RequireAuthorization();

        group.MapGet("/", async (ServiceLinksService services, CancellationToken ct) =>
            Results.Ok(await services.GetAllAsync(ct)));

        group.MapGet("/monitored", async (ServiceLinksService services, CancellationToken ct) =>
            Results.Ok(await services.GetMonitoredAsync(ct)));

        group.MapGet("/health-logs/recent", async (int? take, ServiceLinksService services, CancellationToken ct) =>
            Results.Ok(await services.GetRecentHealthLogsAsync(take ?? 20, ct)));

        group.MapPost("/", async (UpsertServiceLinkRequest request, ServiceLinksService services, CancellationToken ct) =>
            Results.Ok(await services.SaveAsync(request, ct)));

        group.MapDelete("/{serviceLinkId:int}", async (int serviceLinkId, ServiceLinksService services, CancellationToken ct) =>
        {
            await services.DeleteAsync(serviceLinkId, ct);
            return Results.NoContent();
        });

        group.MapPost("/{serviceLinkId:int}/check", async (int serviceLinkId, ServiceLinksService services, CancellationToken ct) =>
            Results.Ok(await services.CheckHealthAsync(serviceLinkId, ct)));
    }
}
