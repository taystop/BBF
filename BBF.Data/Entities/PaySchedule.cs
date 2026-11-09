namespace BBF.Data.Entities;

/// <summary>One earner's regular paycheck (e.g. "Tayler's pay, $2,100 every other Friday into Checking").</summary>
public class PaySchedule
{
    public int Id { get; set; }
    public int? GroupId { get; set; }
    public UserGroup? Group { get; set; }

    /// <summary>The group member who earns this paycheck. Plain column; names are read via group membership.</summary>
    public string UserId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>The normal take-home amount, positive. Overtime shows up as extra when the deposit arrives.</summary>
    public decimal ExpectedAmount { get; set; }

    public string Frequency { get; set; } = "BiWeekly"; // "Weekly", "BiWeekly", "Monthly"

    /// <summary>Any real past or future payday; every other payday is counted from this one.</summary>
    public DateTime FirstPayday { get; set; }

    /// <summary>Next payday that doesn't have a Paycheck row yet (moves forward as paychecks are created).</summary>
    public DateTime NextPayday { get; set; }

    public int? DepositAccountId { get; set; }
    public Account? DepositAccount { get; set; }

    /// <summary>Text the bank description must contain to match the deposit. Null = match on amount and account.</summary>
    public string? MatchText { get; set; }

    /// <summary>Counted in group totals, but only the earner sees the amounts and the deposit.</summary>
    public bool IsPrivate { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
