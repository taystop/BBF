namespace BBF.Shared.Contracts.Recurring;

public record RecurringTransactionDto(
    int Id,
    string Payee,
    decimal Amount,
    int? CategoryId,
    string? CategoryName,
    int AccountId,
    string AccountName,
    string Frequency,
    DateTime NextOccurrence,
    DateTime? EndDate,
    bool IsActive);

public record CreateRecurringTransactionRequest(
    string Payee,
    decimal Amount,
    int? CategoryId,
    int AccountId,
    string Frequency,
    DateTime NextOccurrence,
    DateTime? EndDate);

public record UpdateRecurringTransactionRequest(
    string Payee,
    decimal Amount,
    int? CategoryId,
    int AccountId,
    string Frequency,
    DateTime NextOccurrence,
    DateTime? EndDate,
    bool IsActive);
