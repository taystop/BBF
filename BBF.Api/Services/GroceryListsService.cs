using BBF.Api.Auth;
using BBF.Data.Entities;
using BBF.Shared;
using BBF.Shared.Contracts.MealPlanning;
using Microsoft.EntityFrameworkCore;

namespace BBF.Api.Services;

public class GroceryListsService(ITenantDbContextFactory tenantDb)
{
    public async Task<GroceryListDto?> GetAsync(int groupId, int weekId, CancellationToken ct = default)
    {
        await using var db = await tenantDb.CreateForGroupAsync(groupId, ct);

        // GroceryList carries no GroupId of its own — confirm the week belongs to this tenant first.
        var weekExists = await db.MealPlanWeeks.AnyAsync(w => w.Id == weekId, ct);
        if (!weekExists) return null;

        var list = await db.GroceryLists
            .Include(gl => gl.Items).ThenInclude(i => i.Ingredient)
            .Include(gl => gl.Items).ThenInclude(i => i.Section)
            .FirstOrDefaultAsync(gl => gl.MealPlanWeekId == weekId, ct);
        return list is null ? null : ToDto(list);
    }

    /// <summary>(Re)generates recipe-derived lines from the week's scheduled recipes. Manually-added items and their checked state are left untouched.</summary>
    public async Task<GroceryListDto?> GenerateAsync(int groupId, int weekId, CancellationToken ct = default)
    {
        await using var db = await tenantDb.CreateForGroupAsync(groupId, ct);

        var week = await db.MealPlanWeeks
            .Include(w => w.Entries).ThenInclude(en => en.Recipe).ThenInclude(r => r.Ingredients).ThenInclude(ri => ri.Ingredient)
            .FirstOrDefaultAsync(w => w.Id == weekId, ct);
        if (week is null) return null;

        var list = await db.GroceryLists
            .Include(gl => gl.Items)
            .FirstOrDefaultAsync(gl => gl.MealPlanWeekId == weekId, ct);
        if (list is null)
        {
            list = new GroceryList { MealPlanWeekId = weekId };
            db.GroceryLists.Add(list);
            await db.SaveChangesAsync(ct);
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
        await db.SaveChangesAsync(ct);

        var reloaded = await db.GroceryLists
            .Include(gl => gl.Items).ThenInclude(i => i.Ingredient)
            .Include(gl => gl.Items).ThenInclude(i => i.Section)
            .FirstAsync(gl => gl.Id == list.Id, ct);
        return ToDto(reloaded);
    }

    public async Task AddManualItemAsync(int groupId, int groceryListId, AddManualGroceryItemRequest request, CancellationToken ct = default)
    {
        await using var db = await tenantDb.CreateForGroupAsync(groupId, ct);

        // Required-navigation + filtered-parent trick (see MealPlanWeeksService.UnassignAsync): null
        // MealPlanWeek means the list either doesn't exist or belongs to another tenant.
        var list = await db.GroceryLists.Include(gl => gl.MealPlanWeek).FirstOrDefaultAsync(gl => gl.Id == groceryListId, ct);
        if (list?.MealPlanWeek is null) return;

        db.GroceryListItems.Add(new GroceryListItem
        {
            GroceryListId = groceryListId,
            Description = request.Description,
            SectionId = request.SectionId,
            IsManuallyAdded = true
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task RemoveItemAsync(int groupId, int itemId, CancellationToken ct = default)
    {
        await using var db = await tenantDb.CreateForGroupAsync(groupId, ct);

        var entity = await db.GroceryListItems
            .Include(i => i.GroceryList).ThenInclude(gl => gl.MealPlanWeek)
            .FirstOrDefaultAsync(i => i.Id == itemId, ct);
        if (entity?.GroceryList?.MealPlanWeek is null) return;

        db.GroceryListItems.Remove(entity);
        await db.SaveChangesAsync(ct);
    }

    public async Task SetCheckedAsync(int groupId, int itemId, bool isChecked, CancellationToken ct = default)
    {
        await using var db = await tenantDb.CreateForGroupAsync(groupId, ct);

        var entity = await db.GroceryListItems
            .Include(i => i.GroceryList).ThenInclude(gl => gl.MealPlanWeek)
            .FirstOrDefaultAsync(i => i.Id == itemId, ct);
        if (entity?.GroceryList?.MealPlanWeek is null) return;

        entity.IsChecked = isChecked;
        await db.SaveChangesAsync(ct);
    }

    private static GroceryListDto ToDto(GroceryList gl) => new(
        gl.Id, gl.MealPlanWeekId, gl.GeneratedAt,
        gl.Items.Select(i => new GroceryListItemDto(
            i.Id, i.IngredientId, i.SectionId, i.Section?.Name, i.Description,
            i.RequiredQuantity, i.RequiredUnit, i.PackagesToBuy, i.IsChecked, i.IsManuallyAdded)).ToList());
}
