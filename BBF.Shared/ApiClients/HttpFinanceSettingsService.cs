using System.Net.Http.Json;
using BBF.Shared.Contracts.Finance;

namespace BBF.Shared.ApiClients;

public class HttpFinanceSettingsService(HttpClient http) : IFinanceSettingsService
{
    public async Task<FinanceSettingsDto> GetAsync(int groupId, CancellationToken ct = default) =>
        await http.GetFromJsonAsync<FinanceSettingsDto>($"/api/groups/{groupId}/finance-settings", ct)
        ?? new FinanceSettingsDto(null);

    public async Task UpdateAsync(int groupId, UpdateFinanceSettingsRequest request, CancellationToken ct = default)
    {
        var response = await http.PutAsJsonAsync($"/api/groups/{groupId}/finance-settings", request, ct);
        await response.EnsureSuccessWithErrorBodyAsync(ct);
    }
}
