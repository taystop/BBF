namespace BBF.Shared.Contracts.Plaid;

public interface IPlaidService
{
    /// <summary>Creates a Plaid Link token for the signed-in user (their id comes from the JWT server-side, not the request).</summary>
    Task<string> CreateLinkTokenAsync(CancellationToken ct = default);

    Task<PlaidConnectionDto> ExchangePublicTokenAsync(int groupId, ExchangePublicTokenRequest request, CancellationToken ct = default);
    Task<List<PlaidConnectionDto>> GetConnectionsAsync(int groupId, CancellationToken ct = default);
    Task<SyncTransactionsResult> SyncTransactionsAsync(int groupId, int connectionId, CancellationToken ct = default);
    Task UpdateAccountNameAsync(int groupId, int accountId, UpdatePlaidAccountNameRequest request, CancellationToken ct = default);
    Task RemoveConnectionAsync(int groupId, int connectionId, CancellationToken ct = default);
}
