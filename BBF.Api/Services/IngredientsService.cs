using BBF.Api.Auth;
using BBF.Data.Entities;
using BBF.Shared.Contracts.MealPlanning;
using Microsoft.EntityFrameworkCore;

namespace BBF.Api.Services;

public class IngredientsService(ITenantDbContextFactory tenantDb)
{
    public async Task<List<IngredientDto>> GetIngredientsAsync(int groupId, CancellationToken ct = default)
    {
        await using var db = await tenantDb.CreateForGroupAsync(groupId, ct);
        return await db.Ingredients
            .Include(i => i.Section)
            .OrderBy(i => i.Name)
            .Select(i => new IngredientDto(
                i.Id, i.Name, i.MeasurementType, i.SectionId, i.Section != null ? i.Section.Name : null,
                i.PackageQuantity, i.PackageUnit))
            .ToListAsync(ct);
    }

    public async Task<IngredientDto> CreateAsync(int groupId, CreateIngredientRequest request, CancellationToken ct = default)
    {
        await using var db = await tenantDb.CreateForGroupAsync(groupId, ct);
        var ingredient = new Ingredient
        {
            GroupId = groupId,
            Name = request.Name,
            MeasurementType = request.MeasurementType,
            SectionId = request.SectionId,
            PackageQuantity = request.PackageQuantity,
            PackageUnit = request.PackageUnit,
        };
        db.Ingredients.Add(ingredient);
        await db.SaveChangesAsync(ct);

        await db.Entry(ingredient).Reference(i => i.Section).LoadAsync(ct);
        return ToDto(ingredient);
    }

    public async Task UpdateAsync(int groupId, int ingredientId, UpdateIngredientRequest request, CancellationToken ct = default)
    {
        await using var db = await tenantDb.CreateForGroupAsync(groupId, ct);
        var entity = await db.Ingredients.FirstOrDefaultAsync(i => i.Id == ingredientId, ct);
        if (entity is null) return;

        entity.Name = request.Name;
        entity.MeasurementType = request.MeasurementType;
        entity.SectionId = request.SectionId;
        entity.PackageQuantity = request.PackageQuantity;
        entity.PackageUnit = request.PackageUnit;
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int groupId, int ingredientId, CancellationToken ct = default)
    {
        await using var db = await tenantDb.CreateForGroupAsync(groupId, ct);
        var entity = await db.Ingredients.FirstOrDefaultAsync(i => i.Id == ingredientId, ct);
        if (entity is null) return;

        db.Ingredients.Remove(entity);
        await db.SaveChangesAsync(ct); // throws DbUpdateException on the Restrict FK if still used by a recipe
    }

    private static IngredientDto ToDto(Ingredient i) =>
        new(i.Id, i.Name, i.MeasurementType, i.SectionId, i.Section?.Name, i.PackageQuantity, i.PackageUnit);
}
