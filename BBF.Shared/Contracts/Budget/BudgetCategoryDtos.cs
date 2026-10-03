namespace BBF.Shared.Contracts.Budget;

public record BudgetCategoryDto(
    int Id,
    string Name,
    decimal MonthlyLimit,
    string Color,
    string? Icon,
    int SortOrder,
    bool IsActive,
    string RolloverBehavior,
    int? ParentCategoryId);

public record UpsertBudgetCategoryRequest(
    int Id,
    string Name,
    decimal MonthlyLimit,
    string Color,
    string? Icon,
    int SortOrder,
    bool IsActive,
    string RolloverBehavior,
    int? ParentCategoryId);

/// <summary>A blocked delete (has history, has subcategories) is a normal expected outcome here, not a server error — represented as data rather than an exception.</summary>
public record DeleteBudgetCategoryResult(bool Deleted, string? BlockedReason);
