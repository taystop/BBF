namespace BBF.Shared.Contracts.Finance;

public interface IFinanceSettingsService
{
    Task<FinanceSettingsDto> GetAsync(int groupId, CancellationToken ct = default);
    Task UpdateAsync(int groupId, UpdateFinanceSettingsRequest request, CancellationToken ct = default);
}
