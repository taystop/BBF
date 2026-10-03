using BBF.Api.Services;

namespace BBF.Api.Endpoints;

public static class AmpEndpoints
{
    public static void MapAmpEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/integrations/amp").RequireAuthorization();

        group.MapGet("/instances", async (AmpService amp, CancellationToken ct) =>
            Results.Ok(await amp.GetInstancesAsync(ct)));

        group.MapGet("/instances/{instanceId}/status", async (string instanceId, AmpService amp, CancellationToken ct) =>
        {
            var status = await amp.GetInstanceStatusAsync(instanceId, ct);
            return status is null ? Results.NotFound() : Results.Ok(status);
        });

        group.MapPost("/instances/{instanceId}/start", async (string instanceId, AmpService amp, CancellationToken ct) =>
            Results.Ok(await amp.StartInstanceAsync(instanceId, ct)));

        group.MapPost("/instances/{instanceId}/stop", async (string instanceId, AmpService amp, CancellationToken ct) =>
            Results.Ok(await amp.StopInstanceAsync(instanceId, ct)));

        group.MapPost("/instances/{instanceId}/restart", async (string instanceId, AmpService amp, CancellationToken ct) =>
            Results.Ok(await amp.RestartInstanceAsync(instanceId, ct)));

        group.MapGet("/health", async (AmpService amp, CancellationToken ct) =>
            Results.Ok(await amp.IsHealthyAsync(ct)));
    }
}
