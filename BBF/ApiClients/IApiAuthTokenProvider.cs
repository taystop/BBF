namespace BBF.ApiClients;

public interface IApiAuthTokenProvider
{
    /// <summary>Returns a Bearer token for the current circuit's authenticated user, exchanging and caching one via BBF.Api's internal-token endpoint on first use.</summary>
    Task<string> GetTokenAsync(CancellationToken ct = default);
}
