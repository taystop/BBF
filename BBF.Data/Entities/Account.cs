namespace BBF.Data.Entities;

public class Account
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string AccountType { get; set; } = "Checking"; // "Checking", "Savings", "CreditCard", "Cash", "Investment"
    public decimal StartingBalance { get; set; }
    public decimal CurrentBalance { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Whether purchases made on this account count toward category budgets. Off by default for credit
    /// cards: the household budgets the card PAYMENT (from checking) instead, so counting purchases too
    /// would double count the same money.
    /// </summary>
    public bool CountPurchasesInBudget { get; set; } = true;

    /// <summary>Bank-reported available balance, for bank-linked accounts.</summary>
    public decimal? AvailableBalance { get; set; }

    /// <summary>When CurrentBalance last came from the bank. Null = balance is calculated from transactions.</summary>
    public DateTime? BalanceAsOf { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int? GroupId { get; set; }
    public UserGroup? Group { get; set; }

    public int? PlaidAccountId { get; set; }
    public PlaidAccount? PlaidAccount { get; set; }

    public List<Transaction> Transactions { get; set; } = [];
}
