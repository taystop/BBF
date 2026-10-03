namespace BBF.Data.Entities;

public class MealPlanEntry
{
    public int Id { get; set; }
    public int MealPlanWeekId { get; set; }
    public MealPlanWeek MealPlanWeek { get; set; } = null!;

    public DateTime Date { get; set; } // one of the 7 dates within MealPlanWeek.WeekStartDate's week
    public string MealSlot { get; set; } = "Dinner"; // "Breakfast", "Lunch", "Dinner"

    public int RecipeId { get; set; }
    public Recipe Recipe { get; set; } = null!;

    public int PlannedServings { get; set; }
}
