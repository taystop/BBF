namespace BBF.Shared.Contracts.Transactions;

/// <summary>
/// Client-facing contract both BBF.Web and BBF.Mobile implement as thin HTTP-client wrappers around
/// BBF.Api — the shared .razor pages in BBF.Shared depend only on this interface, never on EF Core or
/// a transport mechanism directly.
/// </summary>
public interface ITransactionService
{
    Task<List<TransactionDto>> GetTransactionsAsync(int groupId, CancellationToken ct = default);
    Task<TransactionDto> CreateAsync(int groupId, CreateTransactionRequest request, CancellationToken ct = default);
    Task UpdateAsync(int groupId, int transactionId, UpdateTransactionRequest request, CancellationToken ct = default);
    Task DeleteAsync(int groupId, int transactionId, CancellationToken ct = default);
    Task<CsvImportResult> ImportCsvAsync(int groupId, int? accountId, Stream csvContent, CancellationToken ct = default);
}
