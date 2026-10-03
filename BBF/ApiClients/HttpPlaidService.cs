using System.Net.Http.Json;
using BBF.Shared.Contracts.Plaid;

namespace BBF.ApiClients;

public class HttpPlaidService(HttpClient http) : IPlaidService
{
    public async Task<string> CreateLinkTokenAsync(CancellationToken ct = default)
    {
        var response = await http.PostAsync("/api/plaid/create-link-token", null, ct);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>(ct);
        return body?["linkToken"] ?? throw new InvalidOperationException("Empty response creating Plaid link token.");
    }

    public async Task<PlaidConnectionDto> ExchangePublicTokenAsync(int groupId, ExchangePublicTokenRequest request, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync($"/api/groups/{groupId}/plaid/exchange-token", request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PlaidConnectionDto>(ct)
            ?? throw new InvalidOperationException("Empty response exchanging Plaid public token.");
    }

    public async Task<List<PlaidConnectionDto>> GetConnectionsAsync(int groupId, CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<PlaidConnectionDto>>($"/api/groups/{groupId}/plaid/connections", ct) ?? [];

    public async Task<SyncTransactionsResult> SyncTransactionsAsync(int groupId, int connectionId, CancellationToken ct = default)
    {
        var response = await http.PostAsync($"/api/groups/{groupId}/plaid/connections/{connectionId}/sync", null, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SyncTransactionsResult>(ct)
            ?? throw new InvalidOperationException("Empty response syncing Plaid transactions.");
    }

    public async Task UpdateAccountNameAsync(int groupId, int accountId, UpdatePlaidAccountNameRequest request, CancellationToken ct = default)
    {
        var response = await http.PutAsJsonAsync($"/api/groups/{groupId}/plaid/accounts/{accountId}/name", request, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task RemoveConnectionAsync(int groupId, int connectionId, CancellationToken ct = default)
    {
        var response = await http.DeleteAsync($"/api/groups/{groupId}/plaid/connections/{connectionId}", ct);
        response.EnsureSuccessStatusCode();
    }
}
