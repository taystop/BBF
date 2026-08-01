namespace BBF.Data.Entities;

public class GoalContribution
{
    public int Id { get; set; }
    public int GoalId { get; set; }
    public Goal Goal { get; set; } = null!;

    public int? TransactionId { get; set; }
    public Transaction? Transaction { get; set; }

    public decimal Amount { get; set; }
    public DateTime ContributedAt { get; set; } = DateTime.UtcNow;
}
