namespace BBF.Shared.Contracts.MealPlanning;

/// <summary>Pure helper the UI needs client-side (week navigation) — no I/O, so no reason to round-trip to BBF.Api for it.</summary>
public static class MealPlanDateHelpers
{
    /// <summary>Weeks always start on Sunday — normalizes any date to that week's Sunday.</summary>
    public static DateTime GetWeekStart(DateTime date) => date.Date.AddDays(-(int)date.Date.DayOfWeek);
}

public record MealPlanEntryDto(int Id, DateTime Date, string MealSlot, int RecipeId, string RecipeName, int PlannedServings);

public record MealPlanWeekDto(int Id, DateTime WeekStartDate, List<MealPlanEntryDto> Entries);

public record AssignRecipeRequest(DateTime Date, string MealSlot, int RecipeId, int PlannedServings);
