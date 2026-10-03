namespace BBF.Shared.Contracts.Transactions;

public record TransactionDto(
    int Id,
    decimal Amount,
    DateTime Date,
    string Description,
    string? MerchantName,
    string Source,
    int? CategoryId,
    string? CategoryName,
    string? CategoryColor,
    int? AccountId,
    string? AccountName,
    int? RecurringTransactionId);

public record CreateTransactionRequest(
    decimal Amount,
    DateTime Date,
    string Description,
    string? MerchantName,
    int? CategoryId,
    int? AccountId);

public record UpdateTransactionRequest(
    decimal Amount,
    DateTime Date,
    string Description,
    string? MerchantName,
    int? CategoryId,
    int? AccountId);

public record CsvImportResult(int Imported, int Skipped);
