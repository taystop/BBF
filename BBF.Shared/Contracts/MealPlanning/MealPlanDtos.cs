namespace BBF.Shared.Contracts.MealPlanning;

public record MealPlanEntryDto(int Id, DateTime Date, string MealSlot, int RecipeId, string RecipeName, int PlannedServings);

public record MealPlanWeekDto(int Id, DateTime WeekStartDate, List<MealPlanEntryDto> Entries);

public record AssignRecipeRequest(DateTime Date, string MealSlot, int RecipeId, int PlannedServings);
