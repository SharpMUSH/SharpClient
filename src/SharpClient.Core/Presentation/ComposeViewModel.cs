using SharpClient.Core.Connection;
using SharpClient.Core.Formatting;
using SharpClient.Core.Platform;
using SharpClient.Core.Sessions;

namespace SharpClient.Core.Presentation;

public sealed class ComposeViewModel
{
    private readonly ISessionManager _manager;
    private readonly IPreferences _prefs;
    private readonly Dictionary<ISession, string> _drafts = [];
    private ISession? _activeSession;
    private PosePrefix _selectedPrefix = PosePrefix.Pose;
    private string _customPrefix = string.Empty;
    private string _pendingDraft = string.Empty;

    public ComposeViewModel(ISessionManager manager, IPreferences prefs)
    {
        _manager = manager;
        _prefs = prefs;
        _manager.Changed += OnManagerChanged;
        TrackActiveSession(_manager.Active);
    }

    internal int TrackedDraftCount => _drafts.Count;

    public event Action? Changed;

    public ISession? Active => _manager.Active;

    public PosePrefix SelectedPrefix
    {
        get => _selectedPrefix;
        set
        {
            _selectedPrefix = value;
            Changed?.Invoke();
        }
    }

    public string CustomPrefix
    {
        get => _customPrefix;
        set
        {
            _customPrefix = value;
            if (Active is not null)
            {
                _prefs.SetString(CustomPrefixKey(Active.WorldId), value);
            }

            Changed?.Invoke();
        }
    }

    public string Body
    {
        get
        {
            if (Active is null)
            {
                return _pendingDraft;
            }

            return _drafts.TryGetValue(Active, out var draft) ? draft : string.Empty;
        }
        set
        {
            if (Active is null)
            {
                _pendingDraft = value;
                Changed?.Invoke();
                return;
            }

            _drafts[Active] = value;
            Changed?.Invoke();
        }
    }

    public string Command => MushPoseFormatter.CommandFor(_selectedPrefix, _customPrefix);

    public string Preview => MushPoseFormatter.Format(Command, Body);

    public bool CanSend =>
        Active?.State == ConnectionState.Connected
        && !string.IsNullOrWhiteSpace(Body)
        && !string.IsNullOrWhiteSpace(Command);

    public async Task SendAsync()
    {
        if (!CanSend || Active is null)
        {
            return;
        }

        var active = Active;
        var line = Preview;
        await active.SendAsync(line);

        _drafts[active] = string.Empty;
        Changed?.Invoke();
    }

    public void Clear()
    {
        if (Active is null)
        {
            return;
        }

        _drafts[Active] = string.Empty;
        Changed?.Invoke();
    }

    internal static string CustomPrefixKey(Guid worldId) => $"compose.custom.{worldId}";

    private void OnManagerChanged()
    {
        var sessions = _manager.Sessions;
        foreach (var key in _drafts.Keys.Where(k => !sessions.Contains(k)).ToList())
        {
            _drafts.Remove(key);
        }

        TrackActiveSession(_manager.Active);
        Changed?.Invoke();
    }

    private void TrackActiveSession(ISession? newActive)
    {
        if (ReferenceEquals(newActive, _activeSession))
        {
            return;
        }

        var hadNoActiveSession = _activeSession is null;

        if (_activeSession is not null)
        {
            _activeSession.StateChanged -= OnActiveStateChanged;
        }

        _activeSession = newActive;

        if (_activeSession is not null)
        {
            _activeSession.StateChanged += OnActiveStateChanged;
            _customPrefix = _prefs.GetString(CustomPrefixKey(_activeSession.WorldId), string.Empty);

            if (hadNoActiveSession && _pendingDraft.Length > 0 && !_drafts.ContainsKey(_activeSession))
            {
                _drafts[_activeSession] = _pendingDraft;
                _pendingDraft = string.Empty;
            }
        }
    }

    private void OnActiveStateChanged(ConnectionState _) => Changed?.Invoke();
}
