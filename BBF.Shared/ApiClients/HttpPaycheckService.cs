using System.Net.Http.Json;
using BBF.Shared.Contracts.Paychecks;

namespace BBF.Shared.ApiClients;

public class HttpPaycheckService(HttpClient http) : IPaycheckService
{
    public async Task<List<PayScheduleDto>> GetSchedulesAsync(int groupId, CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<PayScheduleDto>>($"/api/groups/{groupId}/pay-schedules", ct) ?? [];

    public async Task<PayScheduleDto> CreateScheduleAsync(int groupId, SavePayScheduleRequest request, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync($"/api/groups/{groupId}/pay-schedules", request, ct);
        await response.EnsureSuccessWithErrorBodyAsync(ct);
        return await response.Content.ReadFromJsonAsync<PayScheduleDto>(ct)
            ?? throw new InvalidOperationException("Empty response creating pay schedule.");
    }

    public async Task UpdateScheduleAsync(int groupId, int scheduleId, SavePayScheduleRequest request, CancellationToken ct = default)
    {
        var response = await http.PutAsJsonAsync($"/api/groups/{groupId}/pay-schedules/{scheduleId}", request, ct);
        await response.EnsureSuccessWithErrorBodyAsync(ct);
    }

    public async Task DeleteScheduleAsync(int groupId, int scheduleId, CancellationToken ct = default)
    {
        var response = await http.DeleteAsync($"/api/groups/{groupId}/pay-schedules/{scheduleId}", ct);
        await response.EnsureSuccessWithErrorBodyAsync(ct);
    }

    public async Task<PayPeriodDto> GetPeriodAsync(int groupId, DateTime date, CancellationToken ct = default) =>
        await http.GetFromJsonAsync<PayPeriodDto>($"/api/groups/{groupId}/pay-periods?date={date:yyyy-MM-dd}", ct)
            ?? throw new InvalidOperationException("Empty pay period response.");

    public async Task<List<DateTime>> GetPaydaysAsync(int groupId, DateTime from, DateTime to, CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<DateTime>>($"/api/groups/{groupId}/paydays?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}", ct) ?? [];

    public async Task UpdatePaycheckStatusAsync(int groupId, int paycheckId, UpdatePaycheckStatusRequest request, CancellationToken ct = default)
    {
        var response = await http.PutAsJsonAsync($"/api/groups/{groupId}/paychecks/{paycheckId}/status", request, ct);
        await response.EnsureSuccessWithErrorBodyAsync(ct);
    }

    public async Task MoveBillAsync(int groupId, int billId, MoveBillRequest request, CancellationToken ct = default)
    {
        var response = await http.PutAsJsonAsync($"/api/groups/{groupId}/bills/{billId}/pay-date", request, ct);
        await response.EnsureSuccessWithErrorBodyAsync(ct);
    }
}
