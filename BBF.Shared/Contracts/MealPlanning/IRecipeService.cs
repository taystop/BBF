namespace BBF.Shared.Contracts.MealPlanning;

public interface IRecipeService
{
    Task<List<RecipeDto>> GetRecipesAsync(int groupId, CancellationToken ct = default);
    Task<RecipeDto?> GetRecipeAsync(int groupId, int recipeId, CancellationToken ct = default);
    Task<RecipeDto> CreateAsync(int groupId, CreateRecipeRequest request, CancellationToken ct = default);

    /// <summary>Wholesale-replaces the recipe's ingredient lines with the given list.</summary>
    Task UpdateAsync(int groupId, int recipeId, UpdateRecipeRequest request, CancellationToken ct = default);

    /// <summary>Throws if the recipe is still scheduled on any meal plan entry (Restrict FK) — caller should surface a friendly "unschedule it first" message.</summary>
    Task DeleteAsync(int groupId, int recipeId, CancellationToken ct = default);
}
