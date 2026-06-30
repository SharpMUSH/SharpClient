using SharpClient.Core.Connection;

namespace SharpClient.Core.Sessions;

public interface ISession : IAsyncDisposable
{
    public IReadOnlyList<ScrollbackLine> Scrollback { get; }

    public event Action<ScrollbackLine>? LineAppended;

    public event Action<ConnectionState>? StateChanged;

    public ConnectionState State { get; }

    public Task ConnectAsync(string host, int port, CancellationToken cancellationToken = default);

    public Task SendAsync(string line);
}
