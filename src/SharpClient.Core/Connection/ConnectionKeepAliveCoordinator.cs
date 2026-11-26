using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SharpClient.Core.Sessions;

namespace SharpClient.Core.Connection;

/// <summary>
/// Runs the platform <see cref="IConnectionKeepAlive"/> while any session is connected or on its way
/// back to connected, and halts it a grace period after none are.
/// </summary>
/// <remarks>
/// On Android the keep-alive is a foreground service, and Android 12+ refuses to start one while the
/// app is in the background (screen locked, say). So it must not stop during a reconnect: a session
/// that drops while the phone is locked passes through Reconnecting, or briefly Disconnected on a
/// network-change reconnect, and a keep-alive halted there cannot be started again until the user
/// opens the app. Without it the process loses its wake lock and is frozen, and the session dies.
/// </remarks>
public sealed partial class ConnectionKeepAliveCoordinator : IDisposable
{
    /// <summary>How long the keep-alive outlives the last active session.</summary>
    public static readonly TimeSpan DefaultHaltGrace = TimeSpan.FromSeconds(15);

    private readonly ISessionManager _sessions;
    private readonly IConnectionKeepAlive _keepAlive;
    private readonly Func<CancellationToken, Task> _haltDelay;
    private readonly ILogger _logger;
    private readonly HashSet<ISession> _tracked = [];
    private readonly object _gate = new();
    private string? _status;
    private CancellationTokenSource? _pendingHalt;
    private bool _disposed;

    public ConnectionKeepAliveCoordinator(
        ISessionManager sessions,
        IConnectionKeepAlive keepAlive,
        ILogger<ConnectionKeepAliveCoordinator>? logger = null)
        : this(sessions, keepAlive, token => Task.Delay(DefaultHaltGrace, token), logger)
    {
    }

    /// <param name="haltDelay">Waits out the grace period before a halt; cancelled when a session becomes active again.</param>
    internal ConnectionKeepAliveCoordinator(
        ISessionManager sessions,
        IConnectionKeepAlive keepAlive,
        Func<CancellationToken, Task> haltDelay,
        ILogger? logger = null)
    {
        _sessions = sessions;
        _keepAlive = keepAlive;
        _haltDelay = haltDelay;
        _logger = logger ?? NullLogger.Instance;
        _sessions.Changed += OnSessionsChanged;
        OnSessionsChanged();
    }

    /// <summary>Whether the keep-alive is running, as far as this coordinator knows.</summary>
    public bool IsRunning
    {
        get
        {
            lock (_gate)
            {
                return _status is not null;
            }
        }
    }

    /// <summary>
    /// The device's network changed, so every socket is dead: reconnect each session now instead of
    /// waiting out the backoff. A no-op for a session the user disconnected.
    /// </summary>
    public void ReconnectAll()
    {
        foreach (var session in _sessions.Sessions.ToList())
        {
            _ = ReconnectQuietlyAsync(session);
        }
    }

    private async Task ReconnectQuietlyAsync(ISession session)
    {
        try
        {
            await session.ForceReconnectAsync();
        }
        catch (Exception ex)
        {
            LogReconnectFailed(_logger, session.WorldName, ex);
        }
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

    // Runs inside the connection's state change. Nothing here may throw: an exception would surface
    // in TelnetConnection.SetState and fail the connect or reconnect that raised it.
    private void OnStateChanged(ConnectionState _)
    {
        lock (_gate)
        {
            Evaluate();
        }
    }

    private void Evaluate()
    {
        if (_disposed)
        {
            return;
        }

        var sessions = _sessions.Sessions;
        var connected = sessions.Count(s => s.State == ConnectionState.Connected);
        var pending = sessions.Count(s => s.State is ConnectionState.Connecting or ConnectionState.Reconnecting);

        if (connected + pending == 0)
        {
            ScheduleHalt();
            return;
        }

        CancelPendingHalt();

        var status = connected switch
        {
            0 => "Reconnecting…",
            1 => "1 connection active",
            _ => $"{connected} connections active",
        };

        if (status == _status)
        {
            return;
        }

        try
        {
            _keepAlive.Start(status);
            _status = status;
        }
        catch (Exception ex)
        {
            // Android: ForegroundServiceStartNotAllowedException when this is the first start and the
            // app is in the background. The next state change tries again.
            LogStartFailed(_logger, ex);
        }
    }

    private void ScheduleHalt()
    {
        if (_status is null || _pendingHalt is not null)
        {
            return;
        }

        var cts = new CancellationTokenSource();
        _pendingHalt = cts;
        _ = HaltAfterGraceAsync(cts);
    }

    private async Task HaltAfterGraceAsync(CancellationTokenSource cts)
    {
        try
        {
            await _haltDelay(cts.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        lock (_gate)
        {
            if (!ReferenceEquals(_pendingHalt, cts))
            {
                return;
            }

            _pendingHalt = null;
            cts.Dispose();
            Halt();
        }
    }

    private void CancelPendingHalt()
    {
        if (_pendingHalt is null)
        {
            return;
        }

        _pendingHalt.Cancel();
        _pendingHalt.Dispose();
        _pendingHalt = null;
    }

    private void Halt()
    {
        _status = null;
        try
        {
            _keepAlive.Halt();
        }
        catch (Exception ex)
        {
            LogHaltFailed(_logger, ex);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            CancelPendingHalt();
            _sessions.Changed -= OnSessionsChanged;
            foreach (var session in _tracked)
            {
                session.StateChanged -= OnStateChanged;
            }

            _tracked.Clear();
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Reconnect after a network change failed for {World}")]
    private static partial void LogReconnectFailed(ILogger logger, string world, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not start the connection keep-alive")]
    private static partial void LogStartFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not stop the connection keep-alive")]
    private static partial void LogHaltFailed(ILogger logger, Exception exception);
}
