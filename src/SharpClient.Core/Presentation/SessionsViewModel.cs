using SharpClient.Core.Connection;
using SharpClient.Core.Sessions;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("SharpClient.Tests")]

namespace SharpClient.Core.Presentation;

public sealed class SessionsViewModel
{
    private const int HistoryCap = 20;

    private readonly ISessionManager _manager;
    private readonly Dictionary<ISession, List<string>> _histories = [];
    private ISession? _activeSession;

    public SessionsViewModel(ISessionManager manager)
    {
        _manager = manager;
        _manager.Changed += OnManagerChanged;
        // Wire the initial active session if one is already present.
        TrackActiveSession(_manager.Active);
    }

    internal int TrackedHistoryCount => _histories.Count;

    public IReadOnlyList<ISession> Tabs => _manager.Sessions;

    public ISession? Active => _manager.Active;

    public string Input { get; set; } = string.Empty;

    public bool CanSend => Active?.State == ConnectionState.Connected && !string.IsNullOrWhiteSpace(Input);

    public IReadOnlyList<string> History =>
        Active is not null && _histories.TryGetValue(Active, out var h) ? h : [];

    public event Action? Changed;

    public void Select(ISession session) => _manager.Activate(session);

    public Task CloseAsync(ISession session) => _manager.CloseAsync(session);

    public async Task SendAsync()
    {
        if (!CanSend || Active is null)
            return;

        var command = Input.Trim();
        var active = Active;
        await active.SendAsync(command);

        if (!_histories.TryGetValue(active, out var history))
        {
            history = [];
            _histories[active] = history;
        }

        history.RemoveAll(c => c == command);
        history.Insert(0, command);
        if (history.Count > HistoryCap)
            history.RemoveRange(HistoryCap, history.Count - HistoryCap);

        Input = string.Empty;
        Changed?.Invoke();
    }

    private void OnManagerChanged()
    {
        var sessions = _manager.Sessions;
        foreach (var key in _histories.Keys.Where(k => !sessions.Contains(k)).ToList())
            _histories.Remove(key);

        TrackActiveSession(_manager.Active);
        Changed?.Invoke();
    }

    private void TrackActiveSession(ISession? newActive)
    {
        if (ReferenceEquals(newActive, _activeSession))
            return;

        if (_activeSession is not null)
        {
            _activeSession.LineAppended -= OnActiveLineAppended;
            _activeSession.StateChanged -= OnActiveStateChanged;
        }

        _activeSession = newActive;

        if (_activeSession is not null)
        {
            _activeSession.LineAppended += OnActiveLineAppended;
            _activeSession.StateChanged += OnActiveStateChanged;
        }
    }

    private void OnActiveLineAppended(ScrollbackLine _) => Changed?.Invoke();

    private void OnActiveStateChanged(ConnectionState _) => Changed?.Invoke();
}
