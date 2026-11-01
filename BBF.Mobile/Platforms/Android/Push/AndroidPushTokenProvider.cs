using Android.Gms.Extensions;
using BBF.Mobile.Push;
using Firebase.Messaging;

namespace BBF.Mobile.Platforms.Android.Push;

public class AndroidPushTokenProvider : IPushTokenProvider
{
    public async Task<string?> GetCurrentTokenAsync()
    {
        try
        {
            var token = await FirebaseMessaging.Instance.GetToken();
            return token?.ToString();
        }
        catch
        {
            // No token yet (Play Services unavailable, no network on first launch, etc.) — the app
            // still works, it just won't receive push until a token becomes available.
            return null;
        }
    }
}
