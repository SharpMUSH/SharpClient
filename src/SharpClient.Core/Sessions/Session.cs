using MarkupString;
using MarkupString.Ansi;
using SharpClient.Core.Connection;
using SharpClient.Core.Domain;
using SharpClient.Core.Persistence;
using SharpClient.Core.Platform;
using SharpClient.Core.Rendering;
using SharpClient.Core.Triggers;

namespace SharpClient.Core.Sessions;

/// <summary>One line of output, decoded. <see cref="Rendering.OutputHtml"/> writes it for the output view.</summary>
public sealed record ScrollbackLine(MarkupText Content);

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
    private readonly OutputDecoder _decoder = new();
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
        _connection.PuebloEnabled += OnPuebloEnabled;
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

    public Task ReconnectAsync() => _connection.ReconnectAsync();

    // Bright black (palette 8): dim against the server's own output.
    private static readonly AnsiMarkup EchoStyle = AnsiMarkup.Create(foreground: new AnsiColor.Standard(0, Bright: true));

    public async Task SendAsync(string line)
    {
        var expanded = _aliasEngine is not null && _aliasRules is not null
            ? _aliasEngine.Expand(line, _aliasRules)
            : line;

        // Local echo: MUSH/MUD servers normally don't echo your commands back, so
        // show what was typed in the scrollback (dim, prefixed) for visibility.
        var echo = new ScrollbackLine(MarkupText.Wrap(EchoStyle, "> " + line));
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
        _connection.PuebloEnabled -= OnPuebloEnabled;
        await _connection.DisposeAsync();
    }

    private void OnPuebloEnabled()
    {
        _decoder.EnablePueblo();
        _protocolChanged?.Invoke();
    }

    private void OnMxpEnabled()
    {
        _decoder.EnableMxp();
        _protocolChanged?.Invoke();
    }

    private async void OnLineReceived(string raw)
    {
        // The decoder keeps the style, MXP line mode and open elements from line to line.
        var decoded = _decoder.Decode(raw);

        IReadOnlyList<MarkupText> lines;
        IReadOnlyList<string> sendCommands;
        IReadOnlyList<string> notifications;

        if (_triggerEngine is not null && _triggerRules is not null)
        {
            var outcome = _triggerEngine.Apply(raw, decoded, _triggerRules);
            lines = outcome.Lines;
            sendCommands = outcome.SendCommands;
            notifications = outcome.Notifications;
        }
        else
        {
            lines = decoded;
            sendCommands = [];
            notifications = [];
        }

        foreach (var content in lines)
        {
            var line = new ScrollbackLine(content);
            lock (_scrollbackLock)
            {
                _scrollback.Add(line);
            }

            LineAppended?.Invoke(line);
        }

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

        // MXP and Pueblo belong to one connection: a reconnect negotiates them again, and the
        // server it reaches may not offer either.
        if (previous == ConnectionState.Connected && state != ConnectionState.Connected)
        {
            _decoder.Reset();
        }

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
