using SharpClient.Core.Connection;
using SharpClient.Core.Rendering;

namespace SharpClient.Core.Sessions;

public sealed record ScrollbackLine(IReadOnlyList<StyledSegment> Segments);

public sealed class Session : IAsyncDisposable
{
    private readonly TelnetConnection _connection;
    private readonly List<ScrollbackLine> _scrollback = [];

    public Session(TelnetConnection connection)
    {
        _connection = connection;
        _connection.LineReceived += OnLineReceived;
    }

    public IReadOnlyList<ScrollbackLine> Scrollback => _scrollback;

    public event Action<ScrollbackLine>? LineAppended;

    public ConnectionState State => _connection.State;

    public Task ConnectAsync(string host, int port, CancellationToken cancellationToken = default) =>
        _connection.ConnectAsync(host, port, cancellationToken);

    public Task SendAsync(string line) => _connection.SendAsync(line);

    public async ValueTask DisposeAsync()
    {
        _connection.LineReceived -= OnLineReceived;
        await _connection.DisposeAsync();
    }

    private void OnLineReceived(string raw)
    {
        var line = new ScrollbackLine(AnsiParser.Parse(raw));
        _scrollback.Add(line);
        LineAppended?.Invoke(line);
    }
}
