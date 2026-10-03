namespace BBF.Shared.Contracts.MealPlanning;

public record GrocerySectionDto(int Id, string Name, int SortOrder);

public record CreateGrocerySectionRequest(string Name);

public record UpdateGrocerySectionRequest(string Name);

public record ReorderGrocerySectionsRequest(List<int> OrderedSectionIds);
