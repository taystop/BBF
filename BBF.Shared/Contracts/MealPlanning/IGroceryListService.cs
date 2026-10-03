namespace BBF.Shared.Contracts.MealPlanning;

public interface IGroceryListService
{
    Task<GroceryListDto?> GetAsync(int groupId, int weekId, CancellationToken ct = default);

    /// <summary>(Re)generates recipe-derived lines from the week's scheduled recipes. Manually-added items and their checked state are left untouched.</summary>
    Task<GroceryListDto?> GenerateAsync(int groupId, int weekId, CancellationToken ct = default);

    Task AddManualItemAsync(int groupId, int groceryListId, AddManualGroceryItemRequest request, CancellationToken ct = default);
    Task RemoveItemAsync(int groupId, int itemId, CancellationToken ct = default);
    Task SetCheckedAsync(int groupId, int itemId, bool isChecked, CancellationToken ct = default);
}
