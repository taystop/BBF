using BBF.Api.Auth;
using BBF.Data.Entities;
using BBF.Shared.Contracts.MealPlanning;
using Microsoft.EntityFrameworkCore;

namespace BBF.Api.Services;

public class RecipesService(ITenantDbContextFactory tenantDb)
{
    public async Task<List<RecipeDto>> GetRecipesAsync(int groupId, CancellationToken ct = default)
    {
        await using var db = await tenantDb.CreateForGroupAsync(groupId, ct);
        var recipes = await db.Recipes
            .Include(r => r.Ingredients).ThenInclude(ri => ri.Ingredient)
            .OrderBy(r => r.Name)
            .ToListAsync(ct);
        return recipes.Select(ToDto).ToList();
    }

    public async Task<RecipeDto?> GetRecipeAsync(int groupId, int recipeId, CancellationToken ct = default)
    {
        await using var db = await tenantDb.CreateForGroupAsync(groupId, ct);
        var recipe = await db.Recipes
            .Include(r => r.Ingredients).ThenInclude(ri => ri.Ingredient)
            .FirstOrDefaultAsync(r => r.Id == recipeId, ct);
        return recipe is null ? null : ToDto(recipe);
    }

    public async Task<RecipeDto> CreateAsync(int groupId, CreateRecipeRequest request, CancellationToken ct = default)
    {
        await using var db = await tenantDb.CreateForGroupAsync(groupId, ct);
        var recipe = new Recipe
        {
            GroupId = groupId,
            Name = request.Name,
            BaseServings = request.BaseServings,
            Instructions = request.Instructions,
            Notes = request.Notes,
            Ingredients = request.Ingredients.Select(line => new RecipeIngredient
            {
                IngredientId = line.IngredientId,
                Quantity = line.Quantity,
                Unit = line.Unit,
                Notes = line.Notes,
            }).ToList(),
        };
        db.Recipes.Add(recipe);
        await db.SaveChangesAsync(ct);

        await db.Entry(recipe).Collection(r => r.Ingredients).Query().Include(ri => ri.Ingredient).LoadAsync(ct);
        return ToDto(recipe);
    }

    /// <summary>Wholesale-replaces the recipe's ingredient lines with the given list — simpler than diffing individual RecipeIngredient rows, and safe since nothing else references one by Id.</summary>
    public async Task UpdateAsync(int groupId, int recipeId, UpdateRecipeRequest request, CancellationToken ct = default)
    {
        await using var db = await tenantDb.CreateForGroupAsync(groupId, ct);
        var entity = await db.Recipes
            .Include(r => r.Ingredients)
            .FirstOrDefaultAsync(r => r.Id == recipeId, ct);
        if (entity is null) return;

        entity.Name = request.Name;
        entity.BaseServings = request.BaseServings;
        entity.Instructions = request.Instructions;
        entity.Notes = request.Notes;

        db.RecipeIngredients.RemoveRange(entity.Ingredients);
        foreach (var line in request.Ingredients)
        {
            db.RecipeIngredients.Add(new RecipeIngredient
            {
                RecipeId = recipeId,
                IngredientId = line.IngredientId,
                Quantity = line.Quantity,
                Unit = line.Unit,
                Notes = line.Notes
            });
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>Throws (DbUpdateException, Restrict FK) if the recipe is still scheduled on any meal plan entry.</summary>
    public async Task DeleteAsync(int groupId, int recipeId, CancellationToken ct = default)
    {
        await using var db = await tenantDb.CreateForGroupAsync(groupId, ct);
        var entity = await db.Recipes.FirstOrDefaultAsync(r => r.Id == recipeId, ct);
        if (entity is null) return;

        db.Recipes.Remove(entity);
        await db.SaveChangesAsync(ct);
    }

    private static RecipeDto ToDto(Recipe r) => new(
        r.Id, r.Name, r.BaseServings, r.Instructions, r.Notes,
        r.Ingredients.Select(ri => new RecipeIngredientDto(ri.Id, ri.IngredientId, ri.Ingredient.Name, ri.Quantity, ri.Unit, ri.Notes)).ToList());
}
