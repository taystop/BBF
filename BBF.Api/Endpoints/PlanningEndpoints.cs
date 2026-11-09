using BBF.Api.Services;

namespace BBF.Api.Endpoints;

public static class PlanningEndpoints
{
    public static void MapPlanningEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/groups/{groupId:int}/planning").RequireAuthorization();

        group.MapGet("/month", async (int groupId, DateTime? month, PlanningService planning, CancellationToken ct) =>
            Results.Ok(await planning.GetMonthPlanAsync(groupId, month ?? DateTime.Today, ct)));

        group.MapGet("/review", async (int groupId, DateTime? month, PlanningService planning, CancellationToken ct) =>
            Results.Ok(await planning.GetMonthReviewAsync(groupId, month ?? DateTime.Today, ct)));
    }
}
