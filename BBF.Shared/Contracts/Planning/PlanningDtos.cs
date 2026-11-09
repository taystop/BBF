namespace BBF.Shared.Contracts.Planning;

/// <summary>
/// One month's plan: the paychecks expected in it against what's budgeted and set aside for goals.
/// LeftToAssign = ExpectedIncome − Budgeted − GoalsFunding (negative = budgeted more than will come in).
/// Private paychecks count in the totals but their amounts are hidden in Paydays.
/// </summary>
public record MonthPlanDto(
    DateTime Month,
    bool HasPaySchedules,
    decimal ExpectedIncome,
    decimal ReceivedIncome,
    decimal Budgeted,
    decimal GoalsFunding,
    decimal LeftToAssign,
    List<PlannedPaydayDto> Paydays,
    List<ExtraPaycheckMonthDto> UpcomingExtraPaychecks);

/// <summary>Status: Received, Expected, Skipped, or Projected (further out than paychecks are tracked).</summary>
public record PlannedPaydayDto(
    DateTime Date,
    string Name,
    string EarnerName,
    decimal? Amount,
    string Status,
    bool IsHidden,
    bool IsExtra);

/// <summary>A month with more paydays than usual for someone: a third biweekly or a fifth weekly paycheck.</summary>
public record ExtraPaycheckMonthDto(DateTime Month, List<string> Names, decimal? ExtraAmount);

/// <summary>How a month went. Money in and out are positive; Net = Income − Spending.</summary>
public record MonthReviewDto(
    DateTime Month,
    bool BeforeBudgetStart,
    decimal Income,
    decimal Spending,
    decimal Net,
    decimal MovedToSavings,
    decimal Budgeted,
    decimal UncategorizedSpent,
    List<CategoryReviewDto> Categories,
    List<MonthTrendDto> Trend,
    List<TopMerchantDto> TopMerchants);

/// <summary>Average is the three months before this one (only months inside the budget).</summary>
public record CategoryReviewDto(
    int CategoryId,
    string Name,
    string Color,
    decimal Budgeted,
    decimal Spent,
    decimal? LastMonth,
    decimal? Average);

public record MonthTrendDto(DateTime Month, decimal Income, decimal Spending);

public record TopMerchantDto(string Name, decimal Spent, int Count);

public interface IPlanningService
{
    Task<MonthPlanDto> GetMonthPlanAsync(int groupId, DateTime month, CancellationToken ct = default);
    Task<MonthReviewDto> GetMonthReviewAsync(int groupId, DateTime month, CancellationToken ct = default);
}
