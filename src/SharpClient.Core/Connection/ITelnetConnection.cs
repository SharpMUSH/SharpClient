namespace SharpClient.Core.Connection;

public interface ITelnetConnection : IAsyncDisposable
{
    public event Action<string>? LineReceived;

    public event Action<ConnectionState>? StateChanged;

    public event Action<GmcpMessage>? GmcpReceived;

    public event Action<NegotiationEvent>? NegotiationReceived;

    /// <summary>Raised when MXP (telnet option 91) negotiation completes successfully.</summary>
    public event Action? MxpEnabled;

    public ConnectionState State { get; }

    public Task ConnectAsync(string host, int port, CancellationToken cancellationToken = default);

    /// <summary>
    /// Immediately (re)connects to the last endpoint, dropping the current socket and any reconnect
    /// backoff. Used to recover fast when an external signal (e.g. an Android network-change
    /// callback) indicates connectivity returned. No-op after an intentional disconnect.
    /// </summary>
    public Task ForceReconnectAsync();

    /// <summary>User-initiated reconnect to the last endpoint; works even after an intentional disconnect.</summary>
    public Task ReconnectAsync();

    public Task SendAsync(string line);

    public Task DisconnectAsync();

    public Task SendNawsAsync(int width, int height);
}
