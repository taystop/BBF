namespace BBF.Shared.Contracts.Settings;

/// <summary>The signed-in user's notifications that aren't tied to one budget group.</summary>
public record NotificationSettingsDto(bool ServiceAlerts = true);

public interface IUserSettingsService
{
    Task<NotificationSettingsDto> GetNotificationSettingsAsync(CancellationToken ct = default);
    Task UpdateNotificationSettingsAsync(NotificationSettingsDto settings, CancellationToken ct = default);
}
