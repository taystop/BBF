using BBF.Mobile.Auth;
using BBF.Shared.Contracts.Devices;
using Microsoft.Extensions.DependencyInjection;

namespace BBF.Mobile.Push;

/// <summary>
/// Bridges a device token (from a fresh login, or a native FCM token-refresh callback that runs
/// outside the Blazor/DI component tree) to BBF.Api's device registry. Best-effort: a failed
/// registration just means no push until the next successful attempt (next refresh, or next login)
/// — it never blocks or fails the caller.
/// </summary>
public static class PushTokenRegistrar
{
    public static async Task TryRegisterAsync(string? token)
    {
        if (string.IsNullOrEmpty(token)) return;

        try
        {
            var services = IPlatformApplication.Current?.Services;
            if (services is null) return;

            var auth = services.GetRequiredService<MobileAuthenticationStateProvider>();
            var state = await auth.GetAuthenticationStateAsync();
            if (state.User.Identity?.IsAuthenticated != true) return;

            var devices = services.GetRequiredService<IDeviceService>();
            await devices.RegisterAsync(new RegisterDeviceRequest(token, "Android"));
        }
        catch
        {
        }
    }
}
