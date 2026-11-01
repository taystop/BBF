using Android.App;
using Android.Content;
using Android.Graphics;
using Android.OS;
using AndroidX.Core.App;

namespace BBF.Mobile.Platforms.Android.Push;

/// <summary>Builds and posts the status-bar notification for a received FCM message.</summary>
public static class PushNotificationDisplay
{
    private const string ChannelId = "bbf_default";
    private static int _nextNotificationId = 1000;

    public static void Show(Context context, string title, string body)
    {
        EnsureChannel(context);

        var launchIntent = context.PackageManager?.GetLaunchIntentForPackage(context.PackageName!);
        var pendingIntent = PendingIntent.GetActivity(
            context, 0, launchIntent, PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

        // The status bar always renders SetSmallIcon as a flat white silhouette (Android forces this
        // via the icon's alpha channel, regardless of its actual colors) — the BBF wolf mark on a
        // transparent background. SetLargeIcon is shown in full color in the expanded notification.
        var notification = new NotificationCompat.Builder(context, ChannelId)
            .SetContentTitle(title)
            .SetContentText(body)
            .SetSmallIcon(Resource.Drawable.notification_icon)
            .SetLargeIcon(BitmapFactory.DecodeResource(context.Resources, Resource.Drawable.notification_large_icon))
            .SetAutoCancel(true)
            .SetContentIntent(pendingIntent)
            .Build();

        NotificationManagerCompat.From(context).Notify(Interlocked.Increment(ref _nextNotificationId), notification);
    }

    private static void EnsureChannel(Context context)
    {
        if ((int)Build.VERSION.SdkInt < 26) return;

        var manager = (NotificationManager)context.GetSystemService(Context.NotificationService)!;
        if (manager.GetNotificationChannel(ChannelId) is not null) return;

        var channel = new NotificationChannel(ChannelId, "BBF Notifications", NotificationImportance.Default);
        manager.CreateNotificationChannel(channel);
    }
}
