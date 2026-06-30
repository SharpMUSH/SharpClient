using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;

namespace SharpClient.App;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    private const int PostNotificationsRequestCode = 1001;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // adjustResize is the prerequisite for keyboard handling. On Android 15+ edge-to-edge it no
        // longer resizes the window by itself (the system expects us to consume the IME inset), which
        // WebViewInsetsBridge does via setOnApplyWindowInsetsListener(Type.ime()); we still set it so
        // pre-15 devices resize normally.
        Window?.SetSoftInputMode(SoftInput.AdjustResize);

        RequestPostNotificationsIfNeeded();
    }

    // From Android 13 (API 33) the foreground-service / alert notifications are only shown if the
    // user has granted POST_NOTIFICATIONS at runtime. Request it up front so the keep-alive
    // service's persistent notification and trigger alerts are visible; the app degrades silently
    // if the user declines.
    private void RequestPostNotificationsIfNeeded()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(33))
        {
            return;
        }

        if (CheckSelfPermission(Android.Manifest.Permission.PostNotifications) == Permission.Granted)
        {
            return;
        }

        RequestPermissions([Android.Manifest.Permission.PostNotifications], PostNotificationsRequestCode);
    }
}
