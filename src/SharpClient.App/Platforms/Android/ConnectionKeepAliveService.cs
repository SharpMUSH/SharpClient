using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using SharpClient.Core.Connection;
using System.Runtime.Versioning;

namespace SharpClient.App;

// ── Foreground-service declaration ────────────────────────────────────────────
//
// foregroundServiceType = "connectedDevice"
//   Android docs: "remote device connected through Bluetooth, NFC, IR, USB, or network."
//   A MUSH telnet server is a remote device connected over the network — connectedDevice is the
//   semantically correct type. Crucially, connectedDevice has NO 6-hour time cap (unlike dataSync
//   which is capped at 6 h/24 h on Android 15+), which matters for sessions that last all day.
//
// The Name is pinned to the current ApplicationId to allow the static AndroidManifest.xml to
// reference this service with a stable, predictable android:name for the foregroundServiceType
// attribute (which cannot be set via the C# ServiceAttribute).
// TODO: Update Name when ApplicationId is finalised (currently com.companyname.sharpclient.app).
//
[Service(Exported = false, Name = "com.companyname.sharpclient.app.ConnectionKeepAliveService")]
[SupportedOSPlatform("android26.0")] // Foreground services + NotificationChannel require API 26
internal sealed class ConnectionKeepAliveService : Service, IConnectionKeepAlive
{
    // ── Intent extras & action strings ────────────────────────────────────────

    internal const string ExtraStatus = "extra_status_text";

    // ── Notification constants ────────────────────────────────────────────────

    private const string ChannelId      = "sharpclient_keepalive";
    private const string ChannelName    = "Connection Keep-Alive";
    private const int    NotificationId = 1_001;

    // ── Service lifecycle ─────────────────────────────────────────────────────

    public override IBinder? OnBind(Intent? intent) => null;

    [return: GeneratedEnum]
    public override StartCommandResult OnStartCommand(Intent? intent, [GeneratedEnum] StartCommandFlags flags, int startId)
    {
        EnsureNotificationChannel();
        var status       = intent?.GetStringExtra(ExtraStatus) ?? "Connected";
        var notification = BuildNotification(status);

        // On API 29+ supply the foreground service type declared in AndroidManifest.xml.
        // TypeConnectedDevice = FOREGROUND_SERVICE_TYPE_CONNECTED_DEVICE (connectedDevice; API 29+).
        // On API 26-28, the two-parameter form is still valid (type declaration is API 29+).
        if (OperatingSystem.IsAndroidVersionAtLeast(29))
            StartForeground(NotificationId, notification, Android.Content.PM.ForegroundService.TypeConnectedDevice);
        else
            StartForeground(NotificationId, notification);

        return StartCommandResult.Sticky;
    }

    // ── IConnectionKeepAlive ──────────────────────────────────────────────────

    /// <inheritdoc/>
    public void Start(string statusText)
    {
        var ctx    = Android.App.Application.Context;
        var intent = new Intent(ctx, typeof(ConnectionKeepAliveService))
            .PutExtra(ExtraStatus, statusText);
        // StartForegroundService is API 26+; falls back to StartService on older versions.
        if (OperatingSystem.IsAndroidVersionAtLeast(26))
            ctx.StartForegroundService(intent);
        else
            ctx.StartService(intent);
    }

    /// <inheritdoc/>
    public void Halt()
    {
        var ctx = Android.App.Application.Context;
        ctx.StopService(new Intent(ctx, typeof(ConnectionKeepAliveService)));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void EnsureNotificationChannel()
    {
        var channel = new NotificationChannel(ChannelId, ChannelName, NotificationImportance.Low)
        {
            Description = "Keeps the MUSH TCP connection alive while the app is in the background.",
        };
        var nm = (NotificationManager?)GetSystemService(NotificationService);
        nm?.CreateNotificationChannel(channel);
    }

    private Notification BuildNotification(string statusText) =>
        new Notification.Builder(this, ChannelId)
            .SetContentTitle("SharpClient")
            .SetContentText(statusText)
            .SetSmallIcon(Android.Resource.Drawable.IcDialogInfo) // replaced with branded icon post-stream-1
            .SetOngoing(true)
            .Build()!;
}
