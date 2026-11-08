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
    bool IsActive,
    bool AutoPost = false,
    string? MatchText = null,
    DateTime? NextDue = null);

public record CreateRecurringTransactionRequest(
    string Payee,
    decimal Amount,
    int? CategoryId,
    int AccountId,
    string Frequency,
    DateTime NextOccurrence,
    DateTime? EndDate,
    bool AutoPost = false,
    string? MatchText = null);

public record UpdateRecurringTransactionRequest(
    string Payee,
    decimal Amount,
    int? CategoryId,
    int AccountId,
    string Frequency,
    DateTime NextOccurrence,
    DateTime? EndDate,
    bool IsActive,
    bool AutoPost = false,
    string? MatchText = null);
