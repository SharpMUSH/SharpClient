namespace SharpClient.Core.Connection;

public interface ITelnetConnection : IAsyncDisposable
{
    public event Action<string>? LineReceived;

    public event Action<ConnectionState>? StateChanged;

    public event Action<GmcpMessage>? GmcpReceived;

    public event Action<NegotiationEvent>? NegotiationReceived;

    public ConnectionState State { get; }

    public Task ConnectAsync(string host, int port, CancellationToken cancellationToken = default);

    public Task SendAsync(string line);

    public Task DisconnectAsync();
}
