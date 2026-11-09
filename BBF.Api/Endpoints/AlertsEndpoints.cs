using BBF.Api.Services;
using BBF.Shared.Contracts.Alerts;

namespace BBF.Api.Endpoints;

public static class AlertsEndpoints
{
    public static void MapAlertsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/groups/{groupId:int}").RequireAuthorization();

        group.MapGet("/alerts", async (int groupId, AlertsService alerts, CancellationToken ct) =>
            Results.Ok(await alerts.GetAlertsAsync(groupId, ct)));

        group.MapPost("/alerts/{alertId:int}/dismiss", async (int groupId, int alertId, AlertsService alerts, CancellationToken ct) =>
        {
            await alerts.DismissAsync(groupId, alertId, ct);
            return Results.NoContent();
        });

        group.MapPost("/alerts/dismiss-all", async (int groupId, AlertsService alerts, CancellationToken ct) =>
        {
            await alerts.DismissAllAsync(groupId, ct);
            return Results.NoContent();
        });

        group.MapGet("/alerts/preferences", async (int groupId, AlertsService alerts, CancellationToken ct) =>
            Results.Ok(await alerts.GetPreferencesAsync(groupId, ct)));

        group.MapPut("/alerts/preferences", async (int groupId, AlertPreferencesDto request, AlertsService alerts, CancellationToken ct) =>
        {
            await alerts.UpdatePreferencesAsync(groupId, request, ct);
            return Results.NoContent();
        });

        group.MapPost("/leftover/{decisionId:int}/decision", async (int groupId, int decisionId, LeftoverDecisionRequest request, AlertsService alerts, CancellationToken ct) =>
        {
            try
            {
                await alerts.DecideLeftoverAsync(groupId, decisionId, request, ct);
                return Results.NoContent();
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });
    }
}
