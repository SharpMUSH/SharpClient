using System.Xml.Linq;

namespace SharpClient.Tests.Android;

/// <summary>
/// Guards the Android manifest against the foreground-service crash fixed in ace1651:
/// on Android 15+/targetSDK 35+, starting a foregroundServiceType="connectedDevice" service
/// requires FOREGROUND_SERVICE_CONNECTED_DEVICE *and* at least one companion permission from a
/// fixed set; without one, startForeground() throws SecurityException and the app dies on connect.
/// This test parses the real manifest (no Android build needed) so the requirement can't silently
/// regress.
/// </summary>
public sealed class AndroidManifestForegroundServiceTests
{
    private static readonly XNamespace Android = "http://schemas.android.com/apk/res/android";

    // Companion permissions Android accepts for a connectedDevice FGS (the "anyOf" set the OS
    // enforces). USB is gated by device features rather than a permission, so it's not listed here.
    private static readonly HashSet<string> ConnectedDeviceCompanionPermissions =
    [
        "android.permission.BLUETOOTH_ADVERTISE",
        "android.permission.BLUETOOTH_CONNECT",
        "android.permission.BLUETOOTH_SCAN",
        "android.permission.CHANGE_NETWORK_STATE",
        "android.permission.CHANGE_WIFI_STATE",
        "android.permission.CHANGE_WIFI_MULTICAST_STATE",
        "android.permission.NFC",
        "android.permission.TRANSMIT_IR",
        "android.permission.UWB_RANGING",
        "android.permission.RANGING",
    ];

    private static XDocument LoadManifest()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (; dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(
                dir.FullName, "src", "SharpClient.App", "Platforms", "Android", "AndroidManifest.xml");
            if (File.Exists(candidate))
            {
                return XDocument.Load(candidate);
            }
        }

        throw new FileNotFoundException(
            "Could not locate src/SharpClient.App/Platforms/Android/AndroidManifest.xml by walking up from "
            + AppContext.BaseDirectory);
    }

    private static HashSet<string> DeclaredPermissions(XDocument manifest) =>
        manifest.Root!
            .Elements("uses-permission")
            .Select(e => (string?)e.Attribute(Android + "name"))
            .Where(n => n is not null)
            .Select(n => n!)
            .ToHashSet();

    [Test]
    public async Task ConnectedDeviceServicesDeclareRequiredCompanionPermission()
    {
        var manifest = LoadManifest();
        var permissions = DeclaredPermissions(manifest);

        var connectedDeviceServices = manifest.Descendants("service")
            .Where(s => ((string?)s.Attribute(Android + "foregroundServiceType") ?? string.Empty)
                .Split('|')
                .Contains("connectedDevice"))
            .Select(s => (string?)s.Attribute(Android + "name") ?? "(unnamed)")
            .ToList();

        // Only meaningful if the app actually uses a connectedDevice FGS; if that changes, the
        // test simply passes (the crash class no longer applies).
        if (connectedDeviceServices.Count == 0)
        {
            return;
        }

        await Assert.That(permissions).Contains("android.permission.FOREGROUND_SERVICE_CONNECTED_DEVICE");

        var hasCompanion = permissions.Any(ConnectedDeviceCompanionPermissions.Contains);
        await Assert.That(hasCompanion)
            .IsTrue()
            .Because($"connectedDevice foreground service(s) [{string.Join(", ", connectedDeviceServices)}] "
                + "require at least one companion permission from "
                + string.Join(", ", ConnectedDeviceCompanionPermissions)
                + " or startForeground() throws SecurityException on Android 15+.");
    }

    [Test]
    public async Task AnyForegroundServiceDeclaresBaseForegroundServicePermission()
    {
        var manifest = LoadManifest();
        var hasAnyFgs = manifest.Descendants("service")
            .Any(s => s.Attribute(Android + "foregroundServiceType") is not null);

        if (!hasAnyFgs)
        {
            return;
        }

        await Assert.That(DeclaredPermissions(manifest)).Contains("android.permission.FOREGROUND_SERVICE");
    }
}
