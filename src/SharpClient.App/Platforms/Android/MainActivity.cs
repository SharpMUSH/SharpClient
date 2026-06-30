using Android.App;
using Android.Content.PM;
using Android.OS;

namespace SharpClient.App;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    private const int PostNotificationsRequestCode = 1001;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
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
