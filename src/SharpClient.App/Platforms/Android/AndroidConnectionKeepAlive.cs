using Android.Content;
using SharpClient.Core.Connection;

namespace SharpClient.App;

/// <summary>
/// Android <see cref="IConnectionKeepAlive"/> implementation. Drives
/// <see cref="ConnectionKeepAliveService"/> (a started foreground service) via intents, so it does
/// not depend on holding the OS-managed service instance — the service owns the wake lock and
/// connectivity callback for its own lifetime.
/// </summary>
internal sealed class AndroidConnectionKeepAlive : IConnectionKeepAlive
{
    // Intent extra key shared with ConnectionKeepAliveService. Declared here (no platform
    // annotation) so the API 24+ proxy can reference it without tripping CA1416 against the
    // service's android26.0 SupportedOSPlatform requirement.
    internal const string ExtraStatus = "extra_status_text";

    private bool _started;

    public void Start(string statusText)
    {
        var ctx = Android.App.Application.Context;

        // Android 12+ refuses StartForegroundService while the app is in the background (screen
        // locked), so once the service runs a status change only re-posts its notification.
        if (_started && OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            ConnectionKeepAliveService.UpdateStatus(ctx, statusText);
            return;
        }

        var intent = new Intent(ctx, typeof(ConnectionKeepAliveService))
            .PutExtra(ExtraStatus, statusText);

        // StartForegroundService is API 26+; falls back to StartService on older versions.
        if (OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            ctx.StartForegroundService(intent);
        }
        else
        {
            ctx.StartService(intent);
        }

        _started = true;
    }

    public void Halt()
    {
        _started = false;
        var ctx = Android.App.Application.Context;
        ctx.StopService(new Intent(ctx, typeof(ConnectionKeepAliveService)));
    }
}
