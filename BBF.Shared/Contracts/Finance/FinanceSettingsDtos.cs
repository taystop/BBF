namespace BBF.Shared.Contracts.Finance;

public record FinanceSettingsDto(DateTime? BudgetStartDate);

public record UpdateFinanceSettingsRequest(DateTime? BudgetStartDate);
