namespace SharpClient.Core.Connection;

public interface ITelnetConnection : IAsyncDisposable
{
    public event Action<string>? LineReceived;

    public event Action<ConnectionState>? StateChanged;

    public event Action<GmcpMessage>? GmcpReceived;

    public event Action<NegotiationEvent>? NegotiationReceived;

    /// <summary>Raised when MXP (telnet option 91) negotiation completes successfully.</summary>
    public event Action? MxpEnabled;

    /// <summary>
    /// Raised when the server switches this connection into Pueblo HTML mode, which it does only after
    /// its own hello and our PUEBLOCLIENT answer. A hello quoted in a player's text starts nothing.
    /// </summary>
    public event Action? PuebloEnabled;

    public ConnectionState State { get; }

    /// <summary>Wrap the connection in TLS. Read on every connect and reconnect.</summary>
    public bool UseTls { get; set; }

    /// <summary>
    /// Accept a TLS certificate that fails validation (self-signed, expired, wrong host). Many MUSHes
    /// run TLS with a self-signed certificate, so this is a per-world opt-in, not the default.
    /// </summary>
    public bool AllowInvalidCertificate { get; set; }

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
