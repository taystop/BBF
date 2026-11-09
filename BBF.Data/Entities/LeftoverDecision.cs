namespace BBF.Data.Entities;

/// <summary>
/// What happened to the money left at the end of a pay period. Pending and CarriedOver both add it to
/// the next period (ignoring the question counts as carrying it over); Saved means it went to savings.
/// A negative leftover (overspent) always carries over and is never asked about.
/// </summary>
public class LeftoverDecision
{
    public int Id { get; set; }
    public int? GroupId { get; set; }
    public UserGroup? Group { get; set; }

    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }

    /// <summary>The period this money carries into (the day after PeriodEnd).</summary>
    public DateTime NextPeriodStart { get; set; }

    public decimal Amount { get; set; }
    public string Decision { get; set; } = LeftoverDecisions.Pending;
    public DateTime? DecidedAt { get; set; }
    public string? DecidedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public static class LeftoverDecisions
{
    public const string Pending = "Pending";
    public const string Saved = "Saved";
    public const string CarriedOver = "CarriedOver";
}
