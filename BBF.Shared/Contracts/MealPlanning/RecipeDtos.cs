namespace BBF.Shared.Contracts.MealPlanning;

public record RecipeIngredientDto(int Id, int IngredientId, string IngredientName, decimal Quantity, string Unit, string? Notes);

public record RecipeDto(
    int Id,
    string Name,
    int BaseServings,
    string? Instructions,
    string? Notes,
    List<RecipeIngredientDto> Ingredients);

public record RecipeIngredientLine(int IngredientId, decimal Quantity, string Unit, string? Notes);

public record CreateRecipeRequest(string Name, int BaseServings, string? Instructions, string? Notes, List<RecipeIngredientLine> Ingredients);

public record UpdateRecipeRequest(string Name, int BaseServings, string? Instructions, string? Notes, List<RecipeIngredientLine> Ingredients);
