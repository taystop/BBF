using BBF.Data;
using BBF.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace BBF.Services;

public class RecipeService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;

    public RecipeService(IDbContextFactory<ApplicationDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<List<Recipe>> GetRecipesAsync(int groupId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.Recipes
            .Include(r => r.Ingredients).ThenInclude(ri => ri.Ingredient)
            .Where(r => r.GroupId == groupId)
            .OrderBy(r => r.Name)
            .ToListAsync();
    }

    public async Task<Recipe?> GetRecipeAsync(int recipeId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.Recipes
            .Include(r => r.Ingredients).ThenInclude(ri => ri.Ingredient)
            .FirstOrDefaultAsync(r => r.Id == recipeId);
    }

    public async Task<Recipe> CreateAsync(Recipe recipe)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        db.Recipes.Add(recipe);
        await db.SaveChangesAsync();
        return recipe;
    }

    /// <summary>
    /// Updates recipe fields and wholesale-replaces its ingredient lines with the given list —
    /// simpler than diffing individual RecipeIngredient rows, and safe since nothing else references
    /// a RecipeIngredient by Id.
    /// </summary>
    public async Task UpdateAsync(int recipeId, string name, int baseServings, string? instructions,
        string? notes, List<RecipeIngredient> ingredientLines)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var entity = await db.Recipes
            .Include(r => r.Ingredients)
            .FirstOrDefaultAsync(r => r.Id == recipeId);
        if (entity is null) return;

        entity.Name = name;
        entity.BaseServings = baseServings;
        entity.Instructions = instructions;
        entity.Notes = notes;

        db.RecipeIngredients.RemoveRange(entity.Ingredients);
        foreach (var line in ingredientLines)
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

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Throws if the recipe is still scheduled on any MealPlanEntry (Restrict FK) — the caller should
    /// catch and surface a friendly "unschedule it first" message.
    /// </summary>
    public async Task DeleteAsync(int recipeId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var entity = await db.Recipes.FindAsync(recipeId);
        if (entity is null) return;

        db.Recipes.Remove(entity);
        await db.SaveChangesAsync();
    }
}
