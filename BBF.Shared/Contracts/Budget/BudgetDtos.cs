namespace BBF.Shared.Contracts.Budget;

public record BudgetLineItemDto(
    int Id,
    int CategoryId,
    string CategoryName,
    string CategoryColor,
    decimal BudgetedAmount,
    decimal RolledOverAmount,
    decimal Spent = 0);

public record BudgetPeriodDto(
    int Id,
    DateTime StartDate,
    DateTime EndDate,
    string Status,
    List<BudgetLineItemDto> LineItems,
    decimal UncategorizedSpent = 0);

public record UpsertLineItemRequest(int CategoryId, decimal BudgetedAmount);
