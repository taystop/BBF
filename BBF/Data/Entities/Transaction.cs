namespace BBF.Data.Entities;

public class Transaction
{
    public int Id { get; set; }
    public string? PlaidTransactionId { get; set; }
    public decimal Amount { get; set; }
    public DateTime Date { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? MerchantName { get; set; }
    public string Source { get; set; } = "Manual"; // "Plaid" or "Manual"
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int? CategoryId { get; set; }
    public BudgetCategory? Category { get; set; }

    public int? GroupId { get; set; }
    public UserGroup? Group { get; set; }
}
