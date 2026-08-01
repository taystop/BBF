namespace BBF.Data.Entities;

public class BudgetPeriod
{
    public int Id { get; set; }
    public int? GroupId { get; set; }
    public UserGroup? Group { get; set; }

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; } // exclusive
    public string Status { get; set; } = "Open"; // "Open" or "Closed"
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ClosedAt { get; set; }

    public List<BudgetLineItem> LineItems { get; set; } = [];
}
