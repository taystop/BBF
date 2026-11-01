using Android.App;
using BBF.Mobile.Push;
using Firebase.Messaging;

namespace BBF.Mobile.Platforms.Android.Push;

/// <summary>
/// Entry point FCM calls into directly — before any DI/Blazor context exists. Token refresh goes
/// through PushTokenRegistrar (which reaches into the app's DI container); received messages are
/// just rendered as a plain status-bar notification for v1 (no in-app routing yet).
/// </summary>
[Service(Exported = false)]
[IntentFilter(new[] { "com.google.firebase.MESSAGING_EVENT" })]
public class BbfFirebaseMessagingService : FirebaseMessagingService
{
    public override void OnNewToken(string token)
    {
        base.OnNewToken(token);
        _ = PushTokenRegistrar.TryRegisterAsync(token);
    }

    public override void OnMessageReceived(RemoteMessage message)
    {
        base.OnMessageReceived(message);

        var notification = message.GetNotification();
        message.Data.TryGetValue("title", out var dataTitle);
        message.Data.TryGetValue("body", out var dataBody);
        var title = notification?.Title ?? dataTitle ?? "BBF";
        var body = notification?.Body ?? dataBody ?? "";

        PushNotificationDisplay.Show(ApplicationContext!, title, body);
    }
}
