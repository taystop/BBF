using System.Net.Http.Json;
using BBF.Shared.Contracts.Bills;

namespace BBF.Shared.ApiClients;

public class HttpBillService(HttpClient http) : IBillService
{
    public async Task<List<BillOccurrenceDto>> GetBillsAsync(int groupId, DateTime from, DateTime to, CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<BillOccurrenceDto>>($"/api/groups/{groupId}/bills?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}", ct) ?? [];

    public async Task UpdateStatusAsync(int groupId, int billId, UpdateBillStatusRequest request, CancellationToken ct = default)
    {
        var response = await http.PutAsJsonAsync($"/api/groups/{groupId}/bills/{billId}/status", request, ct);
        await response.EnsureSuccessWithErrorBodyAsync(ct);
    }
}
