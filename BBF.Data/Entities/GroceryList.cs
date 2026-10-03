namespace BBF.Data.Entities;

public class GroceryList
{
    public int Id { get; set; }
    public int MealPlanWeekId { get; set; }
    public MealPlanWeek MealPlanWeek { get; set; } = null!;

    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    public List<GroceryListItem> Items { get; set; } = [];
}
