namespace BBF.Data.Entities;

public class Account
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string AccountType { get; set; } = "Checking"; // "Checking", "Savings", "CreditCard", "Cash", "Investment"
    public decimal StartingBalance { get; set; }
    public decimal CurrentBalance { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int? GroupId { get; set; }
    public UserGroup? Group { get; set; }

    public int? PlaidAccountId { get; set; }
    public PlaidAccount? PlaidAccount { get; set; }

    public List<Transaction> Transactions { get; set; } = [];
}
