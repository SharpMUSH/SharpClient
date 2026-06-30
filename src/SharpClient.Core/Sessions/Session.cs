using SharpClient.Core.Connection;
using SharpClient.Core.Rendering;

namespace SharpClient.Core.Sessions;

public sealed record ScrollbackLine(IReadOnlyList<StyledSegment> Segments);

public sealed class Session : ISession
{
    private readonly ITelnetConnection _connection;

    // NOTE: not thread-safe; LineReceived/protocol events may fire off the network thread —
    // UI consumers must marshal. TODO: guard if accessed concurrently.
    private readonly List<ScrollbackLine> _scrollback = [];
    private readonly List<NegotiationEvent> _negotiationLog = [];
    private readonly List<GmcpMessage> _gmcpLog = [];
    private event Action? _protocolChanged;

    public Session(ITelnetConnection connection, string characterName = "", string worldName = "")
    {
        _connection = connection;
        CharacterName = characterName;
        WorldName = worldName;
        _connection.LineReceived += OnLineReceived;
        _connection.StateChanged += OnStateChanged;
        _connection.GmcpReceived += OnGmcpReceived;
        _connection.NegotiationReceived += OnNegotiationReceived;
    }

    public IReadOnlyList<ScrollbackLine> Scrollback => _scrollback;

    public IReadOnlyList<NegotiationEvent> NegotiationLog => _negotiationLog;

    public IReadOnlyList<GmcpMessage> GmcpLog => _gmcpLog;

    public event Action<ScrollbackLine>? LineAppended;

    public event Action<ConnectionState>? StateChanged;

    public event Action? ProtocolChanged
    {
        add => _protocolChanged += value;
        remove => _protocolChanged -= value;
    }

    public ConnectionState State => _connection.State;

    public string CharacterName { get; }

    public string WorldName { get; }

    public Task ConnectAsync(string host, int port, CancellationToken cancellationToken = default) =>
        _connection.ConnectAsync(host, port, cancellationToken);

    public Task SendAsync(string line) => _connection.SendAsync(line);

    public async ValueTask DisposeAsync()
    {
        _connection.LineReceived -= OnLineReceived;
        _connection.StateChanged -= OnStateChanged;
        _connection.GmcpReceived -= OnGmcpReceived;
        _connection.NegotiationReceived -= OnNegotiationReceived;
        await _connection.DisposeAsync();
    }

    private void OnLineReceived(string raw)
    {
        var line = new ScrollbackLine(AnsiParser.Parse(raw));
        _scrollback.Add(line);
        LineAppended?.Invoke(line);
    }

    private void OnStateChanged(ConnectionState state) => StateChanged?.Invoke(state);

    private void OnGmcpReceived(GmcpMessage msg)
    {
        var idx = _gmcpLog.FindIndex(m => m.Package == msg.Package);
        if (idx >= 0)
            _gmcpLog[idx] = msg;
        else
            _gmcpLog.Add(msg);
        _protocolChanged?.Invoke();
    }

    private void OnNegotiationReceived(NegotiationEvent ev)
    {
        _negotiationLog.Add(ev);
        _protocolChanged?.Invoke();
    }
}
