using SharpClient.Core.Connection;
using SharpClient.Core.Domain;
using SharpClient.Core.Persistence;
using SharpClient.Core.Platform;
using SharpClient.Core.Rendering;
using SharpClient.Core.Triggers;

namespace SharpClient.Core.Sessions;

public sealed record ScrollbackLine(IReadOnlyList<StyledSegment> Segments);

public sealed class Session : ISession
{
    private readonly ITelnetConnection _connection;
    private readonly IAliasEngine? _aliasEngine;
    private readonly IReadOnlyList<AliasRule>? _aliasRules;
    private readonly ITriggerEngine? _triggerEngine;
    private readonly IReadOnlyList<TriggerRule>? _triggerRules;
    private readonly INotifier? _notifier;
    private readonly ISessionHistory? _history;

    // NOTE: not thread-safe; LineReceived/protocol events may fire off the network thread —
    // UI consumers must marshal. TODO: guard if accessed concurrently.
    private readonly List<ScrollbackLine> _scrollback = [];
    private readonly List<NegotiationEvent> _negotiationLog = [];
    private readonly List<GmcpMessage> _gmcpLog = [];
    private event Action? _protocolChanged;

    public Session(
        ITelnetConnection connection,
        string characterName = "",
        string worldName = "",
        Guid worldId = default,
        Guid characterId = default,
        IAliasEngine? aliasEngine = null,
        IReadOnlyList<AliasRule>? aliasRules = null,
        ITriggerEngine? triggerEngine = null,
        IReadOnlyList<TriggerRule>? triggerRules = null,
        INotifier? notifier = null,
        ISessionHistory? history = null)
    {
        _connection = connection;
        CharacterName = characterName;
        WorldName = worldName;
        WorldId = worldId;
        CharacterId = characterId;
        _aliasEngine = aliasEngine;
        _aliasRules = aliasRules;
        _triggerEngine = triggerEngine;
        _triggerRules = triggerRules;
        _notifier = notifier;
        _history = history;
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

    public Guid WorldId { get; }

    public Guid CharacterId { get; }

    public Task ConnectAsync(string host, int port, CancellationToken cancellationToken = default) =>
        _connection.ConnectAsync(host, port, cancellationToken);

    public Task SendAsync(string line)
    {
        var expanded = _aliasEngine is not null && _aliasRules is not null
            ? _aliasEngine.Expand(line, _aliasRules)
            : line;
        return _connection.SendAsync(expanded);
    }

    public Task SendWindowSizeAsync(int cols, int rows) =>
        _connection.SendNawsAsync(cols, rows);

    public async ValueTask DisposeAsync()
    {
        _connection.LineReceived -= OnLineReceived;
        _connection.StateChanged -= OnStateChanged;
        _connection.GmcpReceived -= OnGmcpReceived;
        _connection.NegotiationReceived -= OnNegotiationReceived;
        await _connection.DisposeAsync();
    }

    private async void OnLineReceived(string raw)
    {
        IReadOnlyList<StyledSegment> segments;
        IReadOnlyList<string> sendCommands;
        IReadOnlyList<string> notifications;

        if (_triggerEngine is not null && _triggerRules is not null)
        {
            var outcome = _triggerEngine.Apply(raw, _triggerRules);
            segments = outcome.Segments;
            sendCommands = outcome.SendCommands;
            notifications = outcome.Notifications;
        }
        else
        {
            segments = AnsiParser.Parse(raw);
            sendCommands = [];
            notifications = [];
        }

        var line = new ScrollbackLine(segments);
        _scrollback.Add(line);
        LineAppended?.Invoke(line);

        foreach (var cmd in sendCommands)
        {
            await _connection.SendAsync(cmd);
        }

        if (_notifier is not null)
        {
            foreach (var msg in notifications)
            {
                await _notifier.NotifyAsync(msg);
            }
        }

        if (_history is not null)
        {
            await _history.AppendAsync(CharacterId, raw);
        }
    }

    private void OnStateChanged(ConnectionState state) => StateChanged?.Invoke(state);

    private void OnGmcpReceived(GmcpMessage msg)
    {
        var idx = _gmcpLog.FindIndex(m => m.Package == msg.Package);
        if (idx >= 0)
        {
            _gmcpLog[idx] = msg;
        }
        else
        {
            _gmcpLog.Add(msg);
        }

        _protocolChanged?.Invoke();
    }

    private void OnNegotiationReceived(NegotiationEvent ev)
    {
        _negotiationLog.Add(ev);
        _protocolChanged?.Invoke();
    }
}
