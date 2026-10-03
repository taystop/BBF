using BBF.Api.Services;
using BBF.Shared.Contracts.Goals;

namespace BBF.Api.Endpoints;

public static class GoalsEndpoints
{
    public static void MapGoalsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/groups/{groupId:int}/goals").RequireAuthorization();

        group.MapGet("/", async (int groupId, GoalsService goals, CancellationToken ct) =>
            Results.Ok(await goals.GetGoalsAsync(groupId, ct)));

        group.MapPost("/", async (int groupId, CreateGoalRequest request, GoalsService goals, CancellationToken ct) =>
            Results.Ok(await goals.CreateAsync(groupId, request, ct)));

        group.MapPut("/{goalId:int}", async (int groupId, int goalId, UpdateGoalRequest request, GoalsService goals, CancellationToken ct) =>
        {
            await goals.UpdateAsync(groupId, goalId, request, ct);
            return Results.NoContent();
        });

        group.MapPost("/{goalId:int}/active", async (int groupId, int goalId, bool isActive, GoalsService goals, CancellationToken ct) =>
        {
            await goals.SetActiveAsync(groupId, goalId, isActive, ct);
            return Results.NoContent();
        });

        group.MapPost("/{goalId:int}/contributions", async (int groupId, int goalId, AddGoalContributionRequest request, GoalsService goals, CancellationToken ct) =>
        {
            await goals.AddContributionAsync(groupId, goalId, request, ct);
            return Results.NoContent();
        });
    }
}
