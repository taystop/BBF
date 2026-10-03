namespace BBF.Shared.Contracts.Budget;

public interface IBudgetCategoryService
{
    /// <summary>Active categories only, ordered by SortOrder — matches the original Budget.razor's list exactly (inactive categories aren't browsable there today).</summary>
    Task<List<BudgetCategoryDto>> GetCategoriesAsync(int groupId, CancellationToken ct = default);

    Task<BudgetCategoryDto> CreateAsync(int groupId, UpsertBudgetCategoryRequest request, CancellationToken ct = default);
    Task UpdateAsync(int groupId, int categoryId, UpsertBudgetCategoryRequest request, CancellationToken ct = default);
    Task<DeleteBudgetCategoryResult> DeleteAsync(int groupId, int categoryId, CancellationToken ct = default);
}
