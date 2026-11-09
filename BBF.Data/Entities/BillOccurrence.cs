namespace BBF.Data.Entities;

/// <summary>One expected instance of a recurring bill (e.g. "Rent, due Dec 1, $1,400").</summary>
public class BillOccurrence
{
    public int Id { get; set; }
    // Plain tenancy column with no FK to UserGroups: an FK here would give SQL Server a second cascade
    // path from UserGroups (via RecurringTransactions), which it rejects.
    public int? GroupId { get; set; }

    public int RecurringTransactionId { get; set; }
    public RecurringTransaction RecurringTransaction { get; set; } = null!;

    public DateTime DueDate { get; set; }
    public decimal ExpectedAmount { get; set; }
    public string Status { get; set; } = BillStatuses.Expected;

    /// <summary>The bank or manual transaction that paid this bill, when matched.</summary>
    public int? TransactionId { get; set; }
    public Transaction? Transaction { get; set; }

    public DateTime? PaidAt { get; set; }

    /// <summary>
    /// The payday this bill is paid from, when moved by hand. Null = the last payday before the due date.
    /// </summary>
    public DateTime? AssignedPayDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public static class BillStatuses
{
    public const string Expected = "Expected";
    public const string Paid = "Paid";
    public const string Skipped = "Skipped";

    public static bool IsValid(string? status) => status is Expected or Paid or Skipped;
}
