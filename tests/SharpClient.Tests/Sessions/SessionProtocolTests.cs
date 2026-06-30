using SharpClient.Core.Connection;
using SharpClient.Core.Sessions;

namespace SharpClient.Tests.Sessions;

public sealed class SessionProtocolTests
{
    [Test]
    public async Task GmcpMessageLandsInGmcpLog()
    {
        var conn = new FakeTelnetConnection();
        await using var session = new Session(conn);

        conn.EmitGmcp(new GmcpMessage("Char.Vitals", """{"hp":100}"""));

        await Assert.That(session.GmcpLog.Count).IsEqualTo(1);
        await Assert.That(session.GmcpLog[0].Package).IsEqualTo("Char.Vitals");
        await Assert.That(session.GmcpLog[0].Json).IsEqualTo("""{"hp":100}""");
    }

    [Test]
    public async Task GmcpDedupesByPackageLastWins()
    {
        var conn = new FakeTelnetConnection();
        await using var session = new Session(conn);

        conn.EmitGmcp(new GmcpMessage("Char.Vitals", """{"hp":100}"""));
        conn.EmitGmcp(new GmcpMessage("Char.Vitals", """{"hp":50}"""));

        await Assert.That(session.GmcpLog.Count).IsEqualTo(1);
        await Assert.That(session.GmcpLog[0].Json).IsEqualTo("""{"hp":50}""");
    }

    [Test]
    public async Task NegotiationEventLandsInNegotiationLog()
    {
        var conn = new FakeTelnetConnection();
        await using var session = new Session(conn);

        conn.EmitNegotiation(new NegotiationEvent("MSSP", "NAME=TestMUD PLAYERS=5"));

        await Assert.That(session.NegotiationLog.Count).IsEqualTo(1);
        await Assert.That(session.NegotiationLog[0].Key).IsEqualTo("MSSP");
        await Assert.That(session.NegotiationLog[0].Detail).IsEqualTo("NAME=TestMUD PLAYERS=5");
    }

    [Test]
    public async Task ProtocolChangedFiresOnGmcp()
    {
        var conn = new FakeTelnetConnection();
        await using var session = new Session(conn);
        var fired = 0;
        session.ProtocolChanged += () => fired++;

        conn.EmitGmcp(new GmcpMessage("Room.Info", "{}"));

        await Assert.That(fired).IsEqualTo(1);
    }

    [Test]
    public async Task ProtocolChangedFiresOnNegotiation()
    {
        var conn = new FakeTelnetConnection();
        await using var session = new Session(conn);
        var fired = 0;
        session.ProtocolChanged += () => fired++;

        conn.EmitNegotiation(new NegotiationEvent("MSSP", "NAME=X"));

        await Assert.That(fired).IsEqualTo(1);
    }

    [Test]
    public async Task MultipleDifferentPackagesAccumulate()
    {
        var conn = new FakeTelnetConnection();
        await using var session = new Session(conn);

        conn.EmitGmcp(new GmcpMessage("Char.Vitals", """{"hp":100}"""));
        conn.EmitGmcp(new GmcpMessage("Room.Info", """{"name":"Library"}"""));

        await Assert.That(session.GmcpLog.Count).IsEqualTo(2);
    }
}
