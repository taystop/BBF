namespace BBF.Shared.Contracts.Accounts;

public interface IAccountService
{
    Task<List<AccountDto>> GetAccountsAsync(int groupId, CancellationToken ct = default);
    Task<AccountDto> CreateAsync(int groupId, CreateAccountRequest request, CancellationToken ct = default);
    Task UpdateAsync(int groupId, int accountId, UpdateAccountRequest request, CancellationToken ct = default);
    Task SetActiveAsync(int groupId, int accountId, bool isActive, CancellationToken ct = default);
}
