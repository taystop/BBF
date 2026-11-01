using System.Net.Http.Json;
using BBF.Shared.Contracts.Accounts;

namespace BBF.Shared.ApiClients;

public class HttpAccountService(HttpClient http) : IAccountService
{
    public async Task<List<AccountDto>> GetAccountsAsync(int groupId, CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<AccountDto>>($"/api/groups/{groupId}/accounts", ct) ?? [];

    public async Task<AccountDto> CreateAsync(int groupId, CreateAccountRequest request, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync($"/api/groups/{groupId}/accounts", request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AccountDto>(ct)
            ?? throw new InvalidOperationException("Empty response creating account.");
    }

    public async Task UpdateAsync(int groupId, int accountId, UpdateAccountRequest request, CancellationToken ct = default)
    {
        var response = await http.PutAsJsonAsync($"/api/groups/{groupId}/accounts/{accountId}", request, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task SetActiveAsync(int groupId, int accountId, bool isActive, CancellationToken ct = default)
    {
        var response = await http.PostAsync($"/api/groups/{groupId}/accounts/{accountId}/active?isActive={isActive}", null, ct);
        response.EnsureSuccessStatusCode();
    }
}
