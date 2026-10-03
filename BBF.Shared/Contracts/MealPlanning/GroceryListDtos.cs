namespace BBF.Shared.Contracts.MealPlanning;

public record GroceryListItemDto(
    int Id,
    int? IngredientId,
    int? SectionId,
    string? SectionName,
    string Description,
    decimal? RequiredQuantity,
    string? RequiredUnit,
    int? PackagesToBuy,
    bool IsChecked,
    bool IsManuallyAdded);

public record GroceryListDto(int Id, int MealPlanWeekId, DateTime GeneratedAt, List<GroceryListItemDto> Items);

public record AddManualGroceryItemRequest(string Description, int? SectionId);
