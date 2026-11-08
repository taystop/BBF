namespace BBF.Shared.Contracts.Bills;

public record BillOccurrenceDto(
    int Id,
    int RecurringTransactionId,
    string Payee,
    string? CategoryName,
    string AccountName,
    DateTime DueDate,
    decimal ExpectedAmount,
    string Status,              // "Expected", "Paid", "Skipped"
    int? TransactionId,
    string? TransactionDescription,
    decimal? TransactionAmount,
    DateTime? TransactionDate);

/// <summary>Status must be "Expected", "Paid" or "Skipped". Setting "Expected" unlinks any matched transaction.</summary>
public record UpdateBillStatusRequest(string Status);
