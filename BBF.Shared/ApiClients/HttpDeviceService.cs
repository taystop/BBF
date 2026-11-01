using System.Net.Http.Json;
using BBF.Shared.Contracts.Devices;

namespace BBF.Shared.ApiClients;

public class HttpDeviceService(HttpClient http) : IDeviceService
{
    public async Task RegisterAsync(RegisterDeviceRequest request, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync("/api/devices/register", request, ct);
        await response.EnsureSuccessWithErrorBodyAsync(ct);
    }

    public async Task UnregisterAsync(string token, CancellationToken ct = default)
    {
        var response = await http.DeleteAsync($"/api/devices/register?token={Uri.EscapeDataString(token)}", ct);
        await response.EnsureSuccessWithErrorBodyAsync(ct);
    }
}
