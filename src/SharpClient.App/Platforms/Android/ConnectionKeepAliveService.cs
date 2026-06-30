using Android.App;
using Android.Content;
using Android.Net;
using Android.OS;
using Android.Runtime;
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
// The Name is pinned to the real ApplicationId (com.sharpmush.sharpclient.app) so the static
// AndroidManifest.xml can reference this service with a stable android:name to attach the
// foregroundServiceType attribute (which cannot be set via the C# ServiceAttribute). It MUST stay
// in lock-step with <ApplicationId> in SharpClient.App.csproj and the <service> entry in the
// manifest, otherwise the merger emits two distinct services and startForeground throws.
//
[Service(Exported = false, Name = "com.sharpmush.sharpclient.app.ConnectionKeepAliveService")]
[SupportedOSPlatform("android26.0")] // Foreground services + NotificationChannel require API 26
internal sealed class ConnectionKeepAliveService : Service
{
    // ── Notification constants ────────────────────────────────────────────────

    private const string ChannelId      = "sharpclient_keepalive";
    private const string ChannelName    = "Connection Keep-Alive";
    private const int    NotificationId = 1_001;
    private const string WakeLockTag    = "SharpClient::KeepAlive";

    // ── Runtime state ─────────────────────────────────────────────────────────

    private PowerManager.WakeLock? _wakeLock;
    private ConnectivityManager? _connectivity;
    private ConnectivityManager.NetworkCallback? _networkCallback;

    // ── Service lifecycle ─────────────────────────────────────────────────────

    public override IBinder? OnBind(Intent? intent) => null;

    [return: GeneratedEnum]
    public override StartCommandResult OnStartCommand(Intent? intent, [GeneratedEnum] StartCommandFlags flags, int startId)
    {
        EnsureNotificationChannel();
        var status       = intent?.GetStringExtra(AndroidConnectionKeepAlive.ExtraStatus) ?? "Connected";
        var notification = BuildNotification(status);

        // On API 29+ supply the foreground service type declared in AndroidManifest.xml.
        // TypeConnectedDevice = FOREGROUND_SERVICE_TYPE_CONNECTED_DEVICE (connectedDevice; API 29+).
        // On API 26-28, the two-parameter form is still valid (type declaration is API 29+).
        if (OperatingSystem.IsAndroidVersionAtLeast(29))
            StartForeground(NotificationId, notification, Android.Content.PM.ForegroundService.TypeConnectedDevice);
        else
            StartForeground(NotificationId, notification);

        AcquireWakeLock();
        RegisterNetworkCallback();

        return StartCommandResult.Sticky;
    }

    public override void OnDestroy()
    {
        UnregisterNetworkCallback();
        ReleaseWakeLock();
        base.OnDestroy();
    }

    // ── Wake lock ─────────────────────────────────────────────────────────────
    //
    // A PARTIAL_WAKE_LOCK keeps the CPU running so the TCP socket and telnet-NOP keepalive keep
    // ticking while the screen is off / the device dozes. Released on OnDestroy (i.e. when the
    // coordinator Halts the service because no sessions remain connected).

    private void AcquireWakeLock()
    {
        if (_wakeLock is not null)
        {
            return;
        }

        var power = (PowerManager?)GetSystemService(PowerService);
        _wakeLock = power?.NewWakeLock(WakeLockFlags.Partial, WakeLockTag);
        _wakeLock?.Acquire();
    }

    private void ReleaseWakeLock()
    {
        if (_wakeLock is { IsHeld: true })
        {
            _wakeLock.Release();
        }

        _wakeLock?.Dispose();
        _wakeLock = null;
    }

    // ── Connectivity monitoring ───────────────────────────────────────────────
    //
    // Telnet reconnection itself is owned by the Core layer's auto-reconnect; this callback only
    // refreshes the persistent notification when connectivity returns so the user sees the service
    // is healthy and the process stays alive across network transitions.

    private void RegisterNetworkCallback()
    {
        if (_networkCallback is not null)
        {
            return;
        }

        _connectivity = (ConnectivityManager?)GetSystemService(ConnectivityService);
        if (_connectivity is null)
        {
            return;
        }

        _networkCallback = new KeepAliveNetworkCallback(this);
        var request = new NetworkRequest.Builder()
            .AddCapability(NetCapability.Internet)!
            .Build();
        _connectivity.RegisterNetworkCallback(request!, _networkCallback);
    }

    private void UnregisterNetworkCallback()
    {
        if (_connectivity is not null && _networkCallback is not null)
        {
            try
            {
                _connectivity.UnregisterNetworkCallback(_networkCallback);
            }
            catch (Java.Lang.IllegalArgumentException)
            {
                // Callback was already unregistered — safe to ignore.
            }
        }

        _networkCallback?.Dispose();
        _networkCallback = null;
        _connectivity = null;
    }

    private void OnNetworkAvailable()
    {
        var nm = (NotificationManager?)GetSystemService(NotificationService);
        nm?.Notify(NotificationId, BuildNotification("Connected"));
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
            .SetSmallIcon(Resource.Drawable.ic_stat_keepalive)
            .SetOngoing(true)
            .Build()!;

    private sealed class KeepAliveNetworkCallback(ConnectionKeepAliveService service)
        : ConnectivityManager.NetworkCallback
    {
        public override void OnAvailable(Network network) => service.OnNetworkAvailable();
    }
}
