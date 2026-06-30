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
    public async Task ReconnectingAndErrorAreDistinctStates()
    {
        var reconnecting = ConnectionState.Reconnecting;
        var error = ConnectionState.Error;
        await Assert.That(reconnecting).IsNotEqualTo(error);
        await Assert.That((int)error).IsEqualTo(4);
    }
}
