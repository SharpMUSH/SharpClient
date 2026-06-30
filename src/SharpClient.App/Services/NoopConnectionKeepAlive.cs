using SharpClient.Core.Connection;

namespace SharpClient.App.Services;

/// <summary>
/// No-op <see cref="IConnectionKeepAlive"/> for non-Android heads, where the process is not subject
/// to Android Doze / foreground-service requirements. Lets the shared
/// <see cref="ConnectionKeepAliveCoordinator"/> resolve the dependency on every platform.
/// </summary>
internal sealed class NoopConnectionKeepAlive : IConnectionKeepAlive
{
    public void Start(string statusText)
    {
    }

    public void Halt()
    {
    }
}
