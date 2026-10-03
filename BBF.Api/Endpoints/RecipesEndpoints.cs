using BBF.Api.Services;
using BBF.Shared.Contracts.MealPlanning;

namespace BBF.Api.Endpoints;

public static class RecipesEndpoints
{
    public static void MapRecipesEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/groups/{groupId:int}/recipes").RequireAuthorization();

        group.MapGet("/", async (int groupId, RecipesService recipes, CancellationToken ct) =>
            Results.Ok(await recipes.GetRecipesAsync(groupId, ct)));

        group.MapGet("/{recipeId:int}", async (int groupId, int recipeId, RecipesService recipes, CancellationToken ct) =>
        {
            var recipe = await recipes.GetRecipeAsync(groupId, recipeId, ct);
            return recipe is null ? Results.NotFound() : Results.Ok(recipe);
        });

        group.MapPost("/", async (int groupId, CreateRecipeRequest request, RecipesService recipes, CancellationToken ct) =>
            Results.Ok(await recipes.CreateAsync(groupId, request, ct)));

        group.MapPut("/{recipeId:int}", async (int groupId, int recipeId, UpdateRecipeRequest request, RecipesService recipes, CancellationToken ct) =>
        {
            await recipes.UpdateAsync(groupId, recipeId, request, ct);
            return Results.NoContent();
        });

        group.MapDelete("/{recipeId:int}", async (int groupId, int recipeId, RecipesService recipes, CancellationToken ct) =>
        {
            await recipes.DeleteAsync(groupId, recipeId, ct);
            return Results.NoContent();
        });
    }
}
