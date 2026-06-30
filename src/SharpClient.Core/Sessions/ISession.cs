using SharpClient.Core.Connection;

namespace SharpClient.Core.Sessions;

public interface ISession : IAsyncDisposable
{
    public IReadOnlyList<ScrollbackLine> Scrollback { get; }

    public event Action<ScrollbackLine>? LineAppended;

    public event Action<ConnectionState>? StateChanged;

    public ConnectionState State { get; }

    public string CharacterName { get; }

    public string WorldName { get; }

    public Guid WorldId => Guid.Empty;

    public Guid CharacterId => Guid.Empty;

    public IReadOnlyList<NegotiationEvent> NegotiationLog => [];

    public IReadOnlyList<GmcpMessage> GmcpLog => [];

    public event Action? ProtocolChanged { add { } remove { } }

    public Task ConnectAsync(string host, int port, CancellationToken cancellationToken = default);

    public Task SendAsync(string line);

    public Task SendWindowSizeAsync(int cols, int rows) => Task.CompletedTask;
}
