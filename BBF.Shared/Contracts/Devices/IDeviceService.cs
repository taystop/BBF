namespace BBF.Shared.Contracts.Devices;

/// <summary>BBF.Web never registers a device (no FCM token on a server) — only BBF.Mobile implements and uses this.</summary>
public interface IDeviceService
{
    Task RegisterAsync(RegisterDeviceRequest request, CancellationToken ct = default);

    Task UnregisterAsync(string token, CancellationToken ct = default);
}
