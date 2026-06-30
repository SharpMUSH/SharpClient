namespace SharpClient.Core.Connection;

/// <summary>
/// Platform-agnostic contract for starting and stopping a connection keep-alive mechanism.
/// Android implements this as a foreground service (see Platforms/Android/ConnectionKeepAliveService.cs).
/// Other platforms may use a background timer or OS-level socket keep-alive.
/// </summary>
/// <remarks>
/// Not registered in DI yet — integration with the session lifecycle, SO_KEEPALIVE, and telnet-NOP
/// keepalive is deferred to the post-stream-1 connectivity pass.
/// See docs/superpowers/plans/2026-06-30-android-connectivity.md for the full strategy.
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
