namespace BBF.Data.Entities;

public class Goal
{
    public int Id { get; set; }
    public int? GroupId { get; set; }
    public UserGroup? Group { get; set; }

    public string Name { get; set; } = string.Empty;

    public int? AccountId { get; set; }
    public Account? Account { get; set; }

    public decimal TargetAmount { get; set; }
    public DateTime? TargetDate { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<GoalContribution> Contributions { get; set; } = [];
}
