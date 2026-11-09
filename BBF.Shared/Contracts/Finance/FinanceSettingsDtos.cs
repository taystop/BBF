namespace BBF.Shared.Contracts.Finance;

public record FinanceSettingsDto(DateTime? BudgetStartDate, decimal? LowBalanceThreshold = null);

public record UpdateFinanceSettingsRequest(DateTime? BudgetStartDate, decimal? LowBalanceThreshold = null);
