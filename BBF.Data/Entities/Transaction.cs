namespace BBF.Data.Entities;

public class Transaction
{
    public int Id { get; set; }
    public string? PlaidTransactionId { get; set; }
    public decimal Amount { get; set; }
    public DateTime Date { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? MerchantName { get; set; }
    public string Source { get; set; } = "Manual"; // "Plaid", "Manual", "CSV", or "Recurring"

    /// <summary>"Spending" (counts toward category budgets, net of refunds), "Income", or "Transfer" (ignored by budgets).</summary>
    public string Kind { get; set; } = TransactionKinds.Spending;

    /// <summary>True once a person picks the Kind by hand, so bank syncs stop re-guessing it.</summary>
    public bool KindSetManually { get; set; }

    public bool IsPending { get; set; }

    /// <summary>Plaid's detailed personal-finance category (e.g. "LOAN_PAYMENTS_CREDIT_CARD_PAYMENT"), when synced.</summary>
    public string? PlaidCategory { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int? CategoryId { get; set; }
    public BudgetCategory? Category { get; set; }

    public int? GroupId { get; set; }
    public UserGroup? Group { get; set; }

    public int? AccountId { get; set; }
    public Account? Account { get; set; }

    public int? RecurringTransactionId { get; set; }
    public RecurringTransaction? RecurringTransaction { get; set; }
}

public static class TransactionKinds
{
    public const string Spending = "Spending";
    public const string Income = "Income";
    public const string Transfer = "Transfer";

    public static bool IsValid(string? kind) => kind is Spending or Income or Transfer;
}
