using BBF.Api.Auth;
using BBF.Data.Entities;
using BBF.Shared.Contracts.MealPlanning;
using Microsoft.EntityFrameworkCore;

namespace BBF.Api.Services;

public class GrocerySectionsService(ITenantDbContextFactory tenantDb)
{
    public async Task<List<GrocerySectionDto>> GetSectionsAsync(int groupId, CancellationToken ct = default)
    {
        await using var db = await tenantDb.CreateForGroupAsync(groupId, ct);
        return await db.GrocerySections
            .OrderBy(s => s.SortOrder)
            .Select(s => new GrocerySectionDto(s.Id, s.Name, s.SortOrder))
            .ToListAsync(ct);
    }

    public async Task<GrocerySectionDto> CreateAsync(int groupId, CreateGrocerySectionRequest request, CancellationToken ct = default)
    {
        await using var db = await tenantDb.CreateForGroupAsync(groupId, ct);
        var maxSortOrder = await db.GrocerySections.Select(s => (int?)s.SortOrder).MaxAsync(ct) ?? -1;

        var section = new GrocerySection { GroupId = groupId, Name = request.Name, SortOrder = maxSortOrder + 1 };
        db.GrocerySections.Add(section);
        await db.SaveChangesAsync(ct);
        return new GrocerySectionDto(section.Id, section.Name, section.SortOrder);
    }

    public async Task UpdateAsync(int groupId, int sectionId, UpdateGrocerySectionRequest request, CancellationToken ct = default)
    {
        await using var db = await tenantDb.CreateForGroupAsync(groupId, ct);
        var entity = await db.GrocerySections.FirstOrDefaultAsync(s => s.Id == sectionId, ct);
        if (entity is null) return;

        entity.Name = request.Name;
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int groupId, int sectionId, CancellationToken ct = default)
    {
        await using var db = await tenantDb.CreateForGroupAsync(groupId, ct);
        var entity = await db.GrocerySections.FirstOrDefaultAsync(s => s.Id == sectionId, ct);
        if (entity is null) return;

        db.GrocerySections.Remove(entity);
        await db.SaveChangesAsync(ct);
    }

    public async Task ReorderAsync(int groupId, ReorderGrocerySectionsRequest request, CancellationToken ct = default)
    {
        await using var db = await tenantDb.CreateForGroupAsync(groupId, ct);
        var sections = await db.GrocerySections.ToDictionaryAsync(s => s.Id, ct);

        for (var i = 0; i < request.OrderedSectionIds.Count; i++)
        {
            if (sections.TryGetValue(request.OrderedSectionIds[i], out var section))
                section.SortOrder = i;
        }

        await db.SaveChangesAsync(ct);
    }
}
