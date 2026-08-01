using BBF.Data;
using BBF.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace BBF.Services;

public class GroceryListService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;

    public GroceryListService(IDbContextFactory<ApplicationDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<GroceryList?> GetAsync(int weekId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.GroceryLists
            .Include(gl => gl.Items).ThenInclude(i => i.Ingredient)
            .Include(gl => gl.Items).ThenInclude(i => i.Section)
            .FirstOrDefaultAsync(gl => gl.MealPlanWeekId == weekId);
    }

    /// <summary>
    /// (Re)generates the recipe-derived lines for a week's grocery list: scales each scheduled recipe's
    /// ingredients by planned servings, sums same-MeasurementType quantities per ingredient, converts to a
    /// display unit, and computes a "buy N" package count where the ingredient has a package size configured.
    /// Manually-added items and their checked state are left untouched; recipe-derived lines are fully
    /// replaced (their checked state resets) since regeneration means the plan changed.
    /// </summary>
    public async Task<GroceryList> GenerateAsync(int weekId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var week = await db.MealPlanWeeks
            .Include(w => w.Entries).ThenInclude(en => en.Recipe).ThenInclude(r => r.Ingredients).ThenInclude(ri => ri.Ingredient)
            .FirstOrDefaultAsync(w => w.Id == weekId);
        if (week is null) throw new InvalidOperationException($"MealPlanWeek {weekId} not found.");

        var list = await db.GroceryLists
            .Include(gl => gl.Items)
            .FirstOrDefaultAsync(gl => gl.MealPlanWeekId == weekId);
        if (list is null)
        {
            list = new GroceryList { MealPlanWeekId = weekId };
            db.GroceryLists.Add(list);
            await db.SaveChangesAsync();
        }
        else
        {
            db.GroceryListItems.RemoveRange(list.Items.Where(i => !i.IsManuallyAdded));
        }

        // (IngredientId, MeasurementType) -> contributing (Quantity in base unit, Unit)
        var buckets = new Dictionary<(int IngredientId, string MeasurementType), List<(decimal BaseQuantity, string Unit)>>();
        var ingredientsById = new Dictionary<int, Ingredient>();

        foreach (var entry in week.Entries)
        {
            var scale = entry.Recipe.BaseServings > 0
                ? entry.PlannedServings / (decimal)entry.Recipe.BaseServings
                : 1m;

            foreach (var line in entry.Recipe.Ingredients)
            {
                ingredientsById[line.IngredientId] = line.Ingredient;
                var measurementType = UnitConversion.MeasurementTypeOf(line.Unit);
                var key = (line.IngredientId, measurementType);

                if (!buckets.TryGetValue(key, out var contributions))
                    buckets[key] = contributions = [];

                contributions.Add((UnitConversion.ToBase(line.Quantity * scale, line.Unit), line.Unit));
            }
        }

        foreach (var ((ingredientId, measurementType), contributions) in buckets)
        {
            var ingredient = ingredientsById[ingredientId];
            var totalBase = contributions.Sum(c => c.BaseQuantity);

            // Display in whichever unit contributed the most to this total — most readable for the user.
            var displayUnit = contributions
                .GroupBy(c => c.Unit)
                .OrderByDescending(g => g.Sum(c => c.BaseQuantity))
                .First().Key;

            int? packagesToBuy = null;
            if (ingredient.PackageQuantity is decimal packageQty && ingredient.PackageUnit is string packageUnit
                && UnitConversion.MeasurementTypeOf(packageUnit) == measurementType)
            {
                var packageBase = UnitConversion.ToBase(packageQty, packageUnit);
                if (packageBase > 0)
                    packagesToBuy = (int)Math.Ceiling(totalBase / packageBase);
            }

            db.GroceryListItems.Add(new GroceryListItem
            {
                GroceryListId = list.Id,
                IngredientId = ingredientId,
                SectionId = ingredient.SectionId,
                Description = ingredient.Name,
                RequiredQuantity = UnitConversion.FromBase(totalBase, displayUnit),
                RequiredUnit = displayUnit,
                PackagesToBuy = packagesToBuy,
                IsManuallyAdded = false,
                IsChecked = false
            });
        }

        list.GeneratedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return await db.GroceryLists
            .Include(gl => gl.Items).ThenInclude(i => i.Ingredient)
            .Include(gl => gl.Items).ThenInclude(i => i.Section)
            .FirstAsync(gl => gl.Id == list.Id);
    }

    public async Task AddManualItemAsync(int groceryListId, string description, int? sectionId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        db.GroceryListItems.Add(new GroceryListItem
        {
            GroceryListId = groceryListId,
            Description = description,
            SectionId = sectionId,
            IsManuallyAdded = true
        });
        await db.SaveChangesAsync();
    }

    public async Task RemoveItemAsync(int itemId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var entity = await db.GroceryListItems.FindAsync(itemId);
        if (entity is null) return;

        db.GroceryListItems.Remove(entity);
        await db.SaveChangesAsync();
    }

    public async Task SetCheckedAsync(int itemId, bool isChecked)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var entity = await db.GroceryListItems.FindAsync(itemId);
        if (entity is null) return;

        entity.IsChecked = isChecked;
        await db.SaveChangesAsync();
    }
}
