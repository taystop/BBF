namespace BBF.Data.Entities;

/// <summary>Per-group budgeting settings. One row per group, created on first save.</summary>
public class FinanceSettings
{
    public int Id { get; set; }
    public int? GroupId { get; set; }
    public UserGroup? Group { get; set; }

    /// <summary>Transactions dated before this are left out of every budget calculation. Null = no cutoff.</summary>
    public DateTime? BudgetStartDate { get; set; }

    /// <summary>Checking balance below this is a "tight" day in the forecast. Null = the default ($200).</summary>
    public decimal? LowBalanceThreshold { get; set; }
}
