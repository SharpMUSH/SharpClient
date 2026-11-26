using SharpClient.Core.Connection;
using SharpClient.Core.Sessions;
using SharpClient.Tests.Sessions;

namespace SharpClient.Tests.Connection;

public sealed class ConnectionKeepAliveCoordinatorTests
{
    private sealed class FakeKeepAlive : IConnectionKeepAlive
    {
        public List<string> Starts { get; } = [];
        public int Halts { get; private set; }
        public Exception? StartFailure { get; set; }

        public void Start(string statusText)
        {
            if (StartFailure is { } ex)
            {
                throw ex;
            }

            Starts.Add(statusText);
        }

        public void Halt() => Halts++;
    }

    // Stands in for the grace period: a halt waits until the test calls Elapse.
    private sealed class ManualDelay
    {
        private TaskCompletionSource _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int Waits { get; private set; }

        public Task WaitAsync(CancellationToken token)
        {
            Waits++;
            _tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            token.Register(() => _tcs.TrySetCanceled(token));
            return _tcs.Task;
        }

        public void Elapse() => _tcs.TrySetResult();
    }

    private static (SessionManager Sessions, FakeKeepAlive KeepAlive, ManualDelay Delay, ConnectionKeepAliveCoordinator Coordinator) Create()
    {
        var sessions = new SessionManager();
        var keepAlive = new FakeKeepAlive();
        var delay = new ManualDelay();
        var coordinator = new ConnectionKeepAliveCoordinator(sessions, keepAlive, delay.WaitAsync);
        return (sessions, keepAlive, delay, coordinator);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++)
        {
            await Task.Delay(10);
        }
    }

    [Test]
    public async Task StartsWhenASessionConnects()
    {
        var (sessions, keepAlive, _, coordinator) = Create();

        sessions.Add(new FakeSession { State = ConnectionState.Connected });

        await Assert.That(keepAlive.Starts).IsEquivalentTo(["1 connection active"]);
        await Assert.That(coordinator.IsRunning).IsTrue();
    }

    [Test]
    public async Task KeepsRunningWhileASessionReconnects()
    {
        var (sessions, keepAlive, delay, _) = Create();
        var session = new FakeSession { State = ConnectionState.Connected };
        sessions.Add(session);

        session.RaiseState(ConnectionState.Reconnecting);
        session.RaiseState(ConnectionState.Connected);

        await Assert.That(keepAlive.Halts).IsEqualTo(0);
        await Assert.That(delay.Waits).IsEqualTo(0);
        await Assert.That(keepAlive.Starts).IsEquivalentTo(["1 connection active", "Reconnecting…", "1 connection active"]);
    }

    [Test]
    public async Task NetworkChangeReconnectPassingThroughDisconnectedDoesNotHalt()
    {
        // ForceReconnectAsync tears the old socket down first, so the session is briefly Disconnected.
        var (sessions, keepAlive, delay, _) = Create();
        var session = new FakeSession { State = ConnectionState.Connected };
        sessions.Add(session);

        session.RaiseState(ConnectionState.Disconnected);
        session.RaiseState(ConnectionState.Connecting);
        session.RaiseState(ConnectionState.Connected);
        delay.Elapse();
        await Task.Delay(50);

        await Assert.That(keepAlive.Halts).IsEqualTo(0);
        await Assert.That(keepAlive.Starts[^1]).IsEqualTo("1 connection active");
    }

    [Test]
    public async Task HaltsOnceTheGracePeriodPassesWithNothingActive()
    {
        var (sessions, keepAlive, delay, coordinator) = Create();
        var session = new FakeSession { State = ConnectionState.Connected };
        sessions.Add(session);

        session.RaiseState(ConnectionState.Disconnected);
        await Assert.That(keepAlive.Halts).IsEqualTo(0);

        delay.Elapse();
        await WaitUntilAsync(() => keepAlive.Halts > 0);

        await Assert.That(keepAlive.Halts).IsEqualTo(1);
        await Assert.That(coordinator.IsRunning).IsFalse();
    }

    [Test]
    public async Task DoesNotRestartForAnUnchangedStatus()
    {
        var (sessions, keepAlive, _, _) = Create();
        var session = new FakeSession { State = ConnectionState.Connected };
        sessions.Add(session);

        sessions.Add(new FakeSession { State = ConnectionState.Disconnected });
        session.RaiseState(ConnectionState.Connected);

        await Assert.That(keepAlive.Starts).IsEquivalentTo(["1 connection active"]);
    }

    [Test]
    public async Task AStartThatThrowsDoesNotReachTheConnection()
    {
        // Android refuses a foreground-service start from the background; that must not fail the
        // reconnect whose state change asked for it.
        var (sessions, keepAlive, _, coordinator) = Create();
        var session = new FakeSession { State = ConnectionState.Disconnected };
        sessions.Add(session);
        keepAlive.StartFailure = new InvalidOperationException("ForegroundServiceStartNotAllowed");

        session.RaiseState(ConnectionState.Connected);

        await Assert.That(coordinator.IsRunning).IsFalse();

        keepAlive.StartFailure = null;
        session.RaiseState(ConnectionState.Reconnecting);

        await Assert.That(keepAlive.Starts).IsEquivalentTo(["Reconnecting…"]);
    }

    [Test]
    public async Task ReconnectAllForcesEverySessionAndSwallowsFailures()
    {
        var (sessions, _, _, coordinator) = Create();
        var a = new FakeSession { ForceReconnectFailure = new IOException("network unreachable") };
        var b = new FakeSession();
        sessions.Add(a);
        sessions.Add(b);

        coordinator.ReconnectAll();
        await Task.Delay(20);

        await Assert.That(a.ForceReconnects).IsEqualTo(1);
        await Assert.That(b.ForceReconnects).IsEqualTo(1);
    }
}
