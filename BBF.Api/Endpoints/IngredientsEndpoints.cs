using BBF.Api.Services;
using BBF.Shared.Contracts.MealPlanning;

namespace BBF.Api.Endpoints;

public static class IngredientsEndpoints
{
    public static void MapIngredientsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/groups/{groupId:int}/ingredients").RequireAuthorization();

        group.MapGet("/", async (int groupId, IngredientsService ingredients, CancellationToken ct) =>
            Results.Ok(await ingredients.GetIngredientsAsync(groupId, ct)));

        group.MapPost("/", async (int groupId, CreateIngredientRequest request, IngredientsService ingredients, CancellationToken ct) =>
            Results.Ok(await ingredients.CreateAsync(groupId, request, ct)));

        group.MapPut("/{ingredientId:int}", async (int groupId, int ingredientId, UpdateIngredientRequest request, IngredientsService ingredients, CancellationToken ct) =>
        {
            await ingredients.UpdateAsync(groupId, ingredientId, request, ct);
            return Results.NoContent();
        });

        group.MapDelete("/{ingredientId:int}", async (int groupId, int ingredientId, IngredientsService ingredients, CancellationToken ct) =>
        {
            await ingredients.DeleteAsync(groupId, ingredientId, ct);
            return Results.NoContent();
        });
    }
}
