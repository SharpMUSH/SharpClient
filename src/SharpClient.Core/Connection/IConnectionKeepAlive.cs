namespace SharpClient.Core.Connection;

/// <summary>
/// Platform-agnostic contract for starting and stopping a connection keep-alive mechanism.
/// Android implements this as a foreground service (see Platforms/Android/ConnectionKeepAliveService.cs).
/// Other platforms may use a background timer or OS-level socket keep-alive.
/// </summary>
/// <remarks>
/// Registered in DI in the App head (Android: a foreground service proxy; other platforms: a no-op)
/// and driven by ConnectionKeepAliveCoordinator off the session lifecycle. Socket-level SO_KEEPALIVE
/// and telnet-NOP keepalive remain owned by the Core connection layer.
/// </remarks>
public interface IConnectionKeepAlive
{
    /// <summary>
    /// Starts the keep-alive mechanism with the given notification / status text shown to the user.
    /// Idempotent: calling Start on an already-running keep-alive just updates the status text.
    /// </summary>
    public void Start(string statusText);

    /// <summary>
    /// Stops the keep-alive mechanism. Safe to call when not running.
    /// </summary>
    public void Halt();
}
