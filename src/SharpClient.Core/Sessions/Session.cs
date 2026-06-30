using SharpClient.Core.Connection;
using SharpClient.Core.Rendering;

namespace SharpClient.Core.Sessions;

public sealed record ScrollbackLine(IReadOnlyList<StyledSegment> Segments);

public sealed class Session : ISession
{
    private readonly ITelnetConnection _connection;
    private readonly List<ScrollbackLine> _scrollback = [];

    public Session(ITelnetConnection connection)
    {
        _connection = connection;
        _connection.LineReceived += OnLineReceived;
        _connection.StateChanged += OnStateChanged;
    }

    public IReadOnlyList<ScrollbackLine> Scrollback => _scrollback;

    public event Action<ScrollbackLine>? LineAppended;

    public event Action<ConnectionState>? StateChanged;

    public ConnectionState State => _connection.State;

    public Task ConnectAsync(string host, int port, CancellationToken cancellationToken = default) =>
        _connection.ConnectAsync(host, port, cancellationToken);

    public Task SendAsync(string line) => _connection.SendAsync(line);

    public async ValueTask DisposeAsync()
    {
        _connection.LineReceived -= OnLineReceived;
        _connection.StateChanged -= OnStateChanged;
        await _connection.DisposeAsync();
    }

    private void OnLineReceived(string raw)
    {
        var line = new ScrollbackLine(AnsiParser.Parse(raw));
        _scrollback.Add(line);
        LineAppended?.Invoke(line);
    }

    private void OnStateChanged(ConnectionState state) => StateChanged?.Invoke(state);
}
