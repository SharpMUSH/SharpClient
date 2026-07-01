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

    // Auto-login command provider (resolves the character's stored connect string). Invoked on
    // every transition into Connected so an auto-reconnected session re-authenticates instead of
    // being left at the server's login screen. Null when the character has no stored credentials.
    private readonly Func<ValueTask<string?>>? _autoLoginProvider;
    private ConnectionState _lastState = ConnectionState.Disconnected;

    // LineReceived fires off the network read thread while Blazor enumerates Scrollback on the
    // render thread — appending mid-enumeration throws "Collection was modified" and kills the UI.
    // _scrollbackLock guards every read and write of _scrollback; the Scrollback getter hands out an
    // immutable snapshot so callers can enumerate freely without holding the lock.
    private readonly object _scrollbackLock = new();
    private readonly List<ScrollbackLine> _scrollback = [];
    private readonly List<NegotiationEvent> _negotiationLog = [];
    private readonly List<GmcpMessage> _gmcpLog = [];
    private readonly MxpParserState _mxp = new();
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
        ISessionHistory? history = null,
        Func<ValueTask<string?>>? autoLoginProvider = null)
    {
        _connection = connection;
        _autoLoginProvider = autoLoginProvider;
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
        _connection.MxpEnabled += OnMxpEnabled;
    }

    public IReadOnlyList<ScrollbackLine> Scrollback
    {
        get
        {
            lock (_scrollbackLock)
            {
                return _scrollback.ToArray();
            }
        }
    }

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

    public Task DisconnectAsync() => _connection.DisconnectAsync();

    public Task ForceReconnectAsync() => _connection.ForceReconnectAsync();

    private static readonly TextStyle EchoStyle = TextStyle.Default with { Foreground = AnsiColor.Indexed(8) };

    public async Task SendAsync(string line)
    {
        var expanded = _aliasEngine is not null && _aliasRules is not null
            ? _aliasEngine.Expand(line, _aliasRules)
            : line;

        // Local echo: MUSH/MUD servers normally don't echo your commands back, so
        // show what was typed in the scrollback (dim, prefixed) for visibility.
        var echo = new ScrollbackLine([new StyledSegment("> " + line, EchoStyle)]);
        lock (_scrollbackLock)
        {
            _scrollback.Add(echo);
        }

        LineAppended?.Invoke(echo);

        await _connection.SendAsync(expanded);
    }

    public Task SendWindowSizeAsync(int cols, int rows) =>
        _connection.SendNawsAsync(cols, rows);

    public async ValueTask DisposeAsync()
    {
        _connection.LineReceived -= OnLineReceived;
        _connection.StateChanged -= OnStateChanged;
        _connection.GmcpReceived -= OnGmcpReceived;
        _connection.NegotiationReceived -= OnNegotiationReceived;
        _connection.MxpEnabled -= OnMxpEnabled;
        await _connection.DisposeAsync();
    }

    private void OnMxpEnabled()
    {
        _mxp.IsMxpActive = true;
        _protocolChanged?.Invoke();
    }

    private async void OnLineReceived(string raw)
    {
        // Pueblo negotiation is banner-based (no telnet option): the server announces
        // itself in plain text and the client replies with PUEBLOCLIENT over the data channel.
        if (!_mxp.IsPuebloActive && raw.Contains("This world is Pueblo", StringComparison.OrdinalIgnoreCase))
        {
            _mxp.IsPuebloActive = true;
            _protocolChanged?.Invoke();
            await _connection.SendAsync("PUEBLOCLIENT 2.01");
        }

        // Reset per-line MXP mode to the negotiated default before parsing this line.
        _mxp.BeginLine();

        IReadOnlyList<StyledSegment> segments;
        IReadOnlyList<string> sendCommands;
        IReadOnlyList<string> notifications;

        if (_triggerEngine is not null && _triggerRules is not null)
        {
            var outcome = _triggerEngine.Apply(raw, _triggerRules, _mxp);
            segments = outcome.Segments;
            sendCommands = outcome.SendCommands;
            notifications = outcome.Notifications;
        }
        else
        {
            segments = AnsiParser.Parse(raw, _mxp);
            sendCommands = [];
            notifications = [];
        }

        var line = new ScrollbackLine(segments);
        lock (_scrollbackLock)
        {
            _scrollback.Add(line);
        }

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

    private void OnStateChanged(ConnectionState state)
    {
        var previous = _lastState;
        _lastState = state;
        StateChanged?.Invoke(state);

        // On every transition into Connected — the initial connect AND each automatic reconnect —
        // re-send the stored auto-login, so a dropped-and-reconnected session lands logged in
        // instead of stranded at the server's login screen.
        if (state == ConnectionState.Connected
            && previous != ConnectionState.Connected
            && _autoLoginProvider is not null)
        {
            _ = SendAutoLoginAsync();
        }
    }

    private async Task SendAutoLoginAsync()
    {
        try
        {
            var command = await _autoLoginProvider!().ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(command))
            {
                await SendAsync(command).ConfigureAwait(false);
            }
        }
        catch
        {
            // Best-effort: a failed auto-login just leaves the user at the login screen to retry.
        }
    }

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
