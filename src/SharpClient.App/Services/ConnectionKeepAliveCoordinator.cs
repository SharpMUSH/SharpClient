using SharpClient.Core.Connection;
using SharpClient.Core.Sessions;

namespace SharpClient.App.Services;

/// <summary>
/// App-level glue that starts the platform <see cref="IConnectionKeepAlive"/> while at least one
/// telnet session is connected and halts it once none remain. Observes the
/// <see cref="ISessionManager"/> and each session's <see cref="ISession.StateChanged"/> so the
/// keep-alive tracks real connection activity without touching Core session logic.
/// </summary>
internal sealed class ConnectionKeepAliveCoordinator : IDisposable
{
    private readonly ISessionManager _sessions;
    private readonly IConnectionKeepAlive _keepAlive;
    private readonly HashSet<ISession> _tracked = [];
    private readonly object _gate = new();
    private bool _running;

    public ConnectionKeepAliveCoordinator(ISessionManager sessions, IConnectionKeepAlive keepAlive)
    {
        _sessions = sessions;
        _keepAlive = keepAlive;
        _sessions.Changed += OnSessionsChanged;
        OnSessionsChanged();
    }

    private void OnSessionsChanged()
    {
        lock (_gate)
        {
            var current = _sessions.Sessions;

            foreach (var session in current)
            {
                if (_tracked.Add(session))
                {
                    session.StateChanged += OnStateChanged;
                }
            }

            foreach (var session in _tracked.Where(t => !current.Contains(t)).ToList())
            {
                session.StateChanged -= OnStateChanged;
                _tracked.Remove(session);
            }

            Evaluate();
        }
    }

    private void OnStateChanged(ConnectionState _)
    {
        lock (_gate)
        {
            Evaluate();
        }
    }

    private void Evaluate()
    {
        var connected = _sessions.Sessions.Count(s => s.State == ConnectionState.Connected);

        if (connected > 0)
        {
            var status = connected == 1
                ? "1 connection active"
                : $"{connected} connections active";
            _keepAlive.Start(status);
            _running = true;
        }
        else if (_running)
        {
            _keepAlive.Halt();
            _running = false;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _sessions.Changed -= OnSessionsChanged;
            foreach (var session in _tracked)
            {
                session.StateChanged -= OnStateChanged;
            }

            _tracked.Clear();
        }
    }
}
