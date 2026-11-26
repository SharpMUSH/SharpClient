namespace SharpClient.App.Services;

/// <summary>
/// Process-wide bridge for "the device's default network changed" events. The Android
/// keep-alive service (which owns the connectivity callback but has no session references)
/// raises it; MauiProgram routes it to <see cref="SharpClient.Core.Connection.ConnectionKeepAliveCoordinator.ReconnectAll"/>,
/// which has the sessions. Keeping it here, not in the Android service type, lets that wiring
/// avoid any Android-only API.
/// On platforms that never raise it, subscribers simply never fire.
/// </summary>
internal static class NetworkChangeSignal
{
    /// <summary>Raised when the active/default network changes (e.g. WiFi↔cellular, tower handoff).</summary>
    public static event Action? Changed;

    public static void RaiseChanged() => Changed?.Invoke();
}
