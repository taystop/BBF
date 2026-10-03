namespace BBF.Data.Entities;

public class MealPlanWeek
{
    public int Id { get; set; }
    public int? GroupId { get; set; }
    public UserGroup? Group { get; set; }

    public DateTime WeekStartDate { get; set; } // always a Sunday

    public List<MealPlanEntry> Entries { get; set; } = [];
    public GroceryList? GroceryList { get; set; }
}
