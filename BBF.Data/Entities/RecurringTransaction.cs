namespace BBF.Data.Entities;

public class RecurringTransaction
{
    public int Id { get; set; }
    public int? GroupId { get; set; }
    public UserGroup? Group { get; set; }

    public string Payee { get; set; } = string.Empty;
    public decimal Amount { get; set; } // same sign convention as Transaction.Amount

    public int? CategoryId { get; set; }
    public BudgetCategory? Category { get; set; }

    public int AccountId { get; set; }
    public Account Account { get; set; } = null!;

    public string Frequency { get; set; } = "Monthly"; // "Weekly", "BiWeekly", "Monthly", "Yearly"
    public DateTime NextOccurrence { get; set; }
    public DateTime? EndDate { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// True = post a transaction automatically on each due date (cash/manual items). False (default) =
    /// create an expected bill that a synced bank transaction marks paid, so the bill is never counted twice.
    /// </summary>
    public bool AutoPost { get; set; }

    /// <summary>Text the bank description must contain to match this bill. Null = use Payee.</summary>
    public string? MatchText { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
