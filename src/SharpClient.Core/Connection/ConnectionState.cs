namespace SharpClient.Core.Connection;

public enum ConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Reconnecting,
    Error,
}
