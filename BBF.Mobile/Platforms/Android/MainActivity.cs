using Android.App;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using AndroidX.Core.View;

namespace BBF.Mobile;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    // Modern Android (API 35+, which this app's workload targets by default) draws edge-to-edge:
    // the WebView's content starts at the literal top pixel, under the status bar, with nothing on
    // the Android side reserving space for system bars. The old opt-out (requesting
    // decorFitsSystemWindows) is being phased out by Google, so instead of fighting that, pad the
    // Activity's own content view by the system bar insets — this makes the WebView's viewport
    // itself start below the status bar and above the nav bar at the native layer, so nothing inside
    // the WebView (fixed MudBlazor app bar, bottom nav, etc.) needs any CSS safe-area handling at all.
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        var rootView = Window?.DecorView.FindViewById(Android.Resource.Id.Content);
        if (rootView is not null)
            ViewCompat.SetOnApplyWindowInsetsListener(rootView, new SystemBarInsetsListener());

        // API 33+ requires runtime consent to show any notification, including FCM pushes — ask once
        // at startup rather than gating it behind login, so it's not tied to app state.
        if ((int)Build.VERSION.SdkInt >= 33
            && ContextCompat.CheckSelfPermission(this, Android.Manifest.Permission.PostNotifications) != Permission.Granted)
        {
            ActivityCompat.RequestPermissions(this, new[] { Android.Manifest.Permission.PostNotifications }, 101);
        }
    }

    private sealed class SystemBarInsetsListener : Java.Lang.Object, IOnApplyWindowInsetsListener
    {
        public WindowInsetsCompat? OnApplyWindowInsets(Android.Views.View? v, WindowInsetsCompat? insets)
        {
            if (v is null || insets is null) return insets;

            var systemBars = insets.GetInsets(WindowInsetsCompat.Type.SystemBars())!;
            v.SetPadding(systemBars.Left, systemBars.Top, systemBars.Right, systemBars.Bottom);
            return insets;
        }
    }
}
