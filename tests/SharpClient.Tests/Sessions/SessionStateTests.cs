using SharpClient.Core.Connection;
using SharpClient.Core.Sessions;

namespace SharpClient.Tests.Sessions;

public sealed class SessionStateTests
{
    [Test]
    public async Task SessionForwardsConnectionStateChanges()
    {
        var fake = new FakeTelnetConnection();
        await using var session = new Session(fake);

        var states = new List<ConnectionState>();
        session.StateChanged += states.Add;

        fake.RaiseState(ConnectionState.Connecting);
        fake.RaiseState(ConnectionState.Error);

        await Assert.That(states).IsEquivalentTo(new[] { ConnectionState.Connecting, ConnectionState.Error });
        await Assert.That(session.State).IsEqualTo(ConnectionState.Error);
    }

    [Test]
    public async Task SessionParsesLinesFromAnyConnection()
    {
        var fake = new FakeTelnetConnection();
        await using var session = new Session(fake);

        fake.Emit("plain");

        await Assert.That(session.Scrollback.Count).IsEqualTo(1);
        await Assert.That(session.Scrollback[0].Segments[0].Text).IsEqualTo("plain");
    }

    [Test]
    public async Task SessionSendsAutoLoginOnConnectAndReconnect()
    {
        var fake = new FakeTelnetConnection();
        await using var session = new Session(
            fake, autoLoginProvider: () => ValueTask.FromResult<string?>("connect Foo secret"));

        await session.ConnectAsync("host", 1);        // fake raises Connected
        await Task.Delay(50);
        // Simulate an unexpected drop + automatic reconnect.
        fake.RaiseState(ConnectionState.Reconnecting);
        fake.RaiseState(ConnectionState.Connected);
        await Task.Delay(50);

        // Login is re-sent on the reconnect, not just the initial connect.
        var expected = new[] { "connect Foo secret", "connect Foo secret" };
        await Assert.That(fake.Sent).IsEquivalentTo(expected);
    }

    [Test]
    public async Task SessionWithoutAutoLoginSendsNothingOnConnect()
    {
        var fake = new FakeTelnetConnection();
        await using var session = new Session(fake);

        await session.ConnectAsync("host", 1);
        await Task.Delay(50);

        await Assert.That(fake.Sent).IsEmpty();
    }

    [Test]
    public async Task ReconnectingAndErrorAreDistinctStates()
    {
        var reconnecting = ConnectionState.Reconnecting;
        var error = ConnectionState.Error;
        await Assert.That(reconnecting).IsNotEqualTo(error);
        await Assert.That((int)error).IsEqualTo(4);
    }
}
