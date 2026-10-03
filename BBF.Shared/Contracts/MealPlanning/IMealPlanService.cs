namespace BBF.Shared.Contracts.MealPlanning;

public interface IMealPlanService
{
    Task<MealPlanWeekDto> GetOrCreateWeekAsync(int groupId, DateTime weekStartDate, CancellationToken ct = default);
    Task AssignRecipeAsync(int groupId, int weekId, AssignRecipeRequest request, CancellationToken ct = default);
    Task UnassignAsync(int groupId, int entryId, CancellationToken ct = default);

    /// <summary>Recipes used in the last 90 days (excluding the given week), most-recently-used first.</summary>
    Task<List<RecipeDto>> GetRecentlyUsedRecipesAsync(int groupId, int excludeWeekId, int take = 10, CancellationToken ct = default);
}
