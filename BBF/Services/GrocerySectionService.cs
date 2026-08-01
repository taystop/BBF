using BBF.Data;
using BBF.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace BBF.Services;

public class GrocerySectionService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;

    public GrocerySectionService(IDbContextFactory<ApplicationDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<List<GrocerySection>> GetSectionsAsync(int groupId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.GrocerySections
            .Where(s => s.GroupId == groupId)
            .OrderBy(s => s.SortOrder)
            .ToListAsync();
    }

    public async Task<GrocerySection> CreateAsync(int groupId, string name)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var maxSortOrder = await db.GrocerySections
            .Where(s => s.GroupId == groupId)
            .Select(s => (int?)s.SortOrder)
            .MaxAsync() ?? -1;

        var section = new GrocerySection { GroupId = groupId, Name = name, SortOrder = maxSortOrder + 1 };
        db.GrocerySections.Add(section);
        await db.SaveChangesAsync();
        return section;
    }

    public async Task UpdateAsync(int sectionId, string name)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var entity = await db.GrocerySections.FindAsync(sectionId);
        if (entity is null) return;

        entity.Name = name;
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int sectionId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var entity = await db.GrocerySections.FindAsync(sectionId);
        if (entity is null) return;

        db.GrocerySections.Remove(entity);
        await db.SaveChangesAsync();
    }

    /// <summary>Persists a new sort order for a group's sections, given the full ordered list of IDs.</summary>
    public async Task ReorderAsync(int groupId, List<int> orderedSectionIds)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var sections = await db.GrocerySections
            .Where(s => s.GroupId == groupId)
            .ToDictionaryAsync(s => s.Id);

        for (var i = 0; i < orderedSectionIds.Count; i++)
        {
            if (sections.TryGetValue(orderedSectionIds[i], out var section))
                section.SortOrder = i;
        }

        await db.SaveChangesAsync();
    }
}
