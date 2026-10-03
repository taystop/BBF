namespace BBF.Shared.Contracts.Budget;

public interface IBudgetPeriodService
{
    /// <summary>Returns the period for the given month, or null if none exists yet — past months are never fabricated.</summary>
    Task<BudgetPeriodDto?> GetPeriodAsync(int groupId, DateTime monthStart, CancellationToken ct = default);

    /// <summary>Gets or creates the current calendar month's period, seeding line items from each active category's MonthlyLimit.</summary>
    Task<BudgetPeriodDto> GetOrCreateCurrentPeriodAsync(int groupId, CancellationToken ct = default);

    Task UpsertLineItemAsync(int groupId, int periodId, UpsertLineItemRequest request, CancellationToken ct = default);

    /// <summary>Closes a period and opens the next one, rolling over remaining amounts per category's RolloverBehavior.</summary>
    Task ClosePeriodAsync(int groupId, int periodId, CancellationToken ct = default);
}
