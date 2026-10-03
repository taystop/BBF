using BBF.Api.Services;
using BBF.Shared.Contracts.MealPlanning;

namespace BBF.Api.Endpoints;

public static class MealPlanEndpoints
{
    public static void MapMealPlanEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/groups/{groupId:int}/meal-plan").RequireAuthorization();

        group.MapGet("/weeks", async (int groupId, DateTime weekStart, MealPlanWeeksService mealPlan, CancellationToken ct) =>
            Results.Ok(await mealPlan.GetOrCreateWeekAsync(groupId, weekStart, ct)));

        group.MapPost("/weeks/{weekId:int}/entries", async (int groupId, int weekId, AssignRecipeRequest request, MealPlanWeeksService mealPlan, CancellationToken ct) =>
        {
            await mealPlan.AssignRecipeAsync(groupId, weekId, request, ct);
            return Results.NoContent();
        });

        group.MapDelete("/entries/{entryId:int}", async (int groupId, int entryId, MealPlanWeeksService mealPlan, CancellationToken ct) =>
        {
            await mealPlan.UnassignAsync(groupId, entryId, ct);
            return Results.NoContent();
        });

        group.MapGet("/weeks/{weekId:int}/recently-used-recipes", async (int groupId, int weekId, int take, MealPlanWeeksService mealPlan, CancellationToken ct) =>
            Results.Ok(await mealPlan.GetRecentlyUsedRecipesAsync(groupId, weekId, take > 0 ? take : 10, ct)));
    }
}
