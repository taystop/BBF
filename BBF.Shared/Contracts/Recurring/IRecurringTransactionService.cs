namespace BBF.Shared.Contracts.Recurring;

public interface IRecurringTransactionService
{
    Task<List<RecurringTransactionDto>> GetActiveAsync(int groupId, CancellationToken ct = default);
    Task<RecurringTransactionDto> CreateAsync(int groupId, CreateRecurringTransactionRequest request, CancellationToken ct = default);
    Task UpdateAsync(int groupId, int ruleId, UpdateRecurringTransactionRequest request, CancellationToken ct = default);
    Task DeleteAsync(int groupId, int ruleId, CancellationToken ct = default);
}
