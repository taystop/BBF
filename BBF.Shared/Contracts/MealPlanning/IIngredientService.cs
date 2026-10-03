namespace BBF.Shared.Contracts.MealPlanning;

public interface IIngredientService
{
    Task<List<IngredientDto>> GetIngredientsAsync(int groupId, CancellationToken ct = default);
    Task<IngredientDto> CreateAsync(int groupId, CreateIngredientRequest request, CancellationToken ct = default);
    Task UpdateAsync(int groupId, int ingredientId, UpdateIngredientRequest request, CancellationToken ct = default);

    /// <summary>Throws if the ingredient is still referenced by any recipe (Restrict FK) — caller should surface a friendly "remove it from recipes first" message.</summary>
    Task DeleteAsync(int groupId, int ingredientId, CancellationToken ct = default);
}
