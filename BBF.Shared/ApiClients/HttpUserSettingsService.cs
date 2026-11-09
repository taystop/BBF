using System.Net.Http.Json;
using BBF.Shared.Contracts.Settings;

namespace BBF.Shared.ApiClients;

public class HttpUserSettingsService(HttpClient http) : IUserSettingsService
{
    public async Task<NotificationSettingsDto> GetNotificationSettingsAsync(CancellationToken ct = default) =>
        await http.GetFromJsonAsync<NotificationSettingsDto>("/api/me/notification-settings", ct) ?? new NotificationSettingsDto();

    public async Task UpdateNotificationSettingsAsync(NotificationSettingsDto settings, CancellationToken ct = default)
    {
        var response = await http.PutAsJsonAsync("/api/me/notification-settings", settings, ct);
        await response.EnsureSuccessWithErrorBodyAsync(ct);
    }
}
