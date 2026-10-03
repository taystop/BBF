using BBF.Api.Auth;
using BBF.Data.Entities;
using BBF.Shared.Contracts.Budget;
using Microsoft.EntityFrameworkCore;

namespace BBF.Api.Services;

public class BudgetCategoriesService(ITenantDbContextFactory tenantDb)
{
    public async Task<List<BudgetCategoryDto>> GetCategoriesAsync(int groupId, CancellationToken ct = default)
    {
        await using var db = await tenantDb.CreateForGroupAsync(groupId, ct);
        return await db.BudgetCategories
            .Where(c => c.IsActive)
            .OrderBy(c => c.SortOrder)
            .Select(c => new BudgetCategoryDto(c.Id, c.Name, c.MonthlyLimit, c.Color, c.Icon, c.SortOrder, c.IsActive, c.RolloverBehavior, c.ParentCategoryId))
            .ToListAsync(ct);
    }

    public async Task<BudgetCategoryDto> CreateAsync(int groupId, UpsertBudgetCategoryRequest request, CancellationToken ct = default)
    {
        await using var db = await tenantDb.CreateForGroupAsync(groupId, ct);
        var category = new BudgetCategory
        {
            GroupId = groupId,
            Name = request.Name,
            MonthlyLimit = request.MonthlyLimit,
            Color = request.Color,
            Icon = request.Icon,
            SortOrder = request.SortOrder,
            IsActive = request.IsActive,
            RolloverBehavior = request.RolloverBehavior,
            ParentCategoryId = request.ParentCategoryId,
        };
        db.BudgetCategories.Add(category);
        await db.SaveChangesAsync(ct);
        return ToDto(category);
    }

    public async Task UpdateAsync(int groupId, int categoryId, UpsertBudgetCategoryRequest request, CancellationToken ct = default)
    {
        await using var db = await tenantDb.CreateForGroupAsync(groupId, ct);
        var entity = await db.BudgetCategories.FirstOrDefaultAsync(c => c.Id == categoryId, ct);
        if (entity is null) return;

        entity.Name = request.Name;
        entity.MonthlyLimit = request.MonthlyLimit;
        entity.Color = request.Color;
        entity.Icon = request.Icon;
        entity.SortOrder = request.SortOrder;
        entity.IsActive = request.IsActive;
        entity.RolloverBehavior = request.RolloverBehavior;
        entity.ParentCategoryId = request.ParentCategoryId;
        await db.SaveChangesAsync(ct);
    }

    public async Task<DeleteBudgetCategoryResult> DeleteAsync(int groupId, int categoryId, CancellationToken ct = default)
    {
        await using var db = await tenantDb.CreateForGroupAsync(groupId, ct);

        // Check existence (tenant-filtered) before the history/subcategory checks below, which query
        // tables with no GroupId filter of their own — avoids leaking even a true/false about a
        // category that doesn't belong to this tenant.
        var entity = await db.BudgetCategories.FirstOrDefaultAsync(c => c.Id == categoryId, ct);
        if (entity is null) return new DeleteBudgetCategoryResult(false, "not found.");

        var hasHistory = await db.BudgetLineItems.AnyAsync(li => li.CategoryId == categoryId, ct);
        if (hasHistory)
            return new DeleteBudgetCategoryResult(false, $"'{entity.Name}' has budget history and can't be deleted. Toggle it inactive instead.");

        var hasSubcategories = await db.BudgetCategories.AnyAsync(c => c.ParentCategoryId == categoryId, ct);
        if (hasSubcategories)
            return new DeleteBudgetCategoryResult(false, $"'{entity.Name}' has subcategories. Reassign or delete them first.");

        db.BudgetCategories.Remove(entity);
        await db.SaveChangesAsync(ct);
        return new DeleteBudgetCategoryResult(true, null);
    }

    private static BudgetCategoryDto ToDto(BudgetCategory c) =>
        new(c.Id, c.Name, c.MonthlyLimit, c.Color, c.Icon, c.SortOrder, c.IsActive, c.RolloverBehavior, c.ParentCategoryId);
}
