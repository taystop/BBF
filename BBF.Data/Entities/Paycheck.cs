namespace BBF.Data.Entities;

/// <summary>One expected payday for a <see cref="PaySchedule"/>, matched to the real deposit when it arrives.</summary>
public class Paycheck
{
    public int Id { get; set; }
    // Plain tenancy column with no FK to UserGroups, for the same cascade-path reason as BillOccurrence.
    public int? GroupId { get; set; }

    public int PayScheduleId { get; set; }
    public PaySchedule PaySchedule { get; set; } = null!;

    public DateTime PayDate { get; set; }
    public decimal ExpectedAmount { get; set; }
    public string Status { get; set; } = PaycheckStatuses.Expected;

    /// <summary>The deposit transaction, once matched.</summary>
    public int? TransactionId { get; set; }
    public Transaction? Transaction { get; set; }

    /// <summary>What actually landed (positive). Anything above ExpectedAmount is overtime or a bonus.</summary>
    public decimal? ActualAmount { get; set; }

    public DateTime? ReceivedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public static class PaycheckStatuses
{
    public const string Expected = "Expected";
    public const string Received = "Received";
    public const string Skipped = "Skipped";

    public static bool IsValid(string? status) => status is Expected or Received or Skipped;
}
