using BBF.Data;
using BBF.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace BBF.Services;

public class IngredientService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;

    public IngredientService(IDbContextFactory<ApplicationDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<List<Ingredient>> GetIngredientsAsync(int groupId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.Ingredients
            .Include(i => i.Section)
            .Where(i => i.GroupId == groupId)
            .OrderBy(i => i.Name)
            .ToListAsync();
    }

    public async Task<Ingredient> CreateAsync(Ingredient ingredient)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        db.Ingredients.Add(ingredient);
        await db.SaveChangesAsync();
        return ingredient;
    }

    public async Task UpdateAsync(int ingredientId, string name, string measurementType, int? sectionId,
        decimal? packageQuantity, string? packageUnit)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var entity = await db.Ingredients.FindAsync(ingredientId);
        if (entity is null) return;

        entity.Name = name;
        entity.MeasurementType = measurementType;
        entity.SectionId = sectionId;
        entity.PackageQuantity = packageQuantity;
        entity.PackageUnit = packageUnit;
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Throws if the ingredient is still referenced by any RecipeIngredient (Restrict FK) — the caller
    /// should catch and surface a friendly "remove it from recipes first" message.
    /// </summary>
    public async Task DeleteAsync(int ingredientId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var entity = await db.Ingredients.FindAsync(ingredientId);
        if (entity is null) return;

        db.Ingredients.Remove(entity);
        await db.SaveChangesAsync();
    }
}
