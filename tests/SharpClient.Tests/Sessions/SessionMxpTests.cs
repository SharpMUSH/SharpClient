using SharpClient.Core.Sessions;

namespace SharpClient.Tests.Sessions;

public sealed class SessionMxpTests
{
    [Test]
    public async Task MxpSecureSendBecomesClickableSegment()
    {
        var conn = new FakeTelnetConnection();
        await using var session = new Session(conn);

        conn.EnableMxp();
        conn.Emit("\e[1z<SEND href=\"look\">Look here</SEND>");

        var line = session.Scrollback[^1];
        await Assert.That(line.Segments[0].Text).IsEqualTo("Look here");
        await Assert.That(line.Segments[0].Command).IsEqualTo("look");
    }

    [Test]
    public async Task SendIsNotClickableWhenMxpNeverNegotiated()
    {
        var conn = new FakeTelnetConnection();
        await using var session = new Session(conn);

        // No EnableMxp(): markup must pass through inert, never clickable.
        conn.Emit("\e[1z<SEND href=\"rm -rf\">click</SEND>");

        await Assert.That(session.Scrollback[^1].Segments[0].Command).IsNull();
    }

    [Test]
    public async Task PuebloModeFromTheServerEnablesLinks()
    {
        var conn = new FakeTelnetConnection();
        await using var session = new Session(conn);

        conn.EnablePueblo();
        conn.Emit("<a xch_cmd=\"go north\">North</a>");

        var line = session.Scrollback[^1];
        await Assert.That(line.Segments[0].Text).IsEqualTo("North");
        await Assert.That(line.Segments[0].Command).IsEqualTo("go north");
    }

    [Test]
    public async Task QuotedPuebloHelloDoesNotEnablePueblo()
    {
        var conn = new FakeTelnetConnection();
        await using var session = new Session(conn);

        // A player can say the hello. Only the server's start sequence (via TNC) enables Pueblo, so
        // the anchor that follows must stay inert and nothing may be sent.
        conn.Emit("Bob says, \"This world is Pueblo 1.10 Enhanced.\"");
        conn.Emit("<a xch_cmd=\"@destroy me\">free stuff</a>");

        await Assert.That(conn.Sent).IsEmpty();
        await Assert.That(session.Scrollback[^1].Segments.All(s => s.Command is null)).IsTrue();
    }

    [Test]
    public async Task MarkupStateIsDroppedWhenTheConnectionGoesDown()
    {
        var conn = new FakeTelnetConnection();
        await using var session = new Session(conn);

        conn.RaiseState(SharpClient.Core.Connection.ConnectionState.Connected);
        conn.EnablePueblo();
        conn.RaiseState(SharpClient.Core.Connection.ConnectionState.Reconnecting);
        conn.RaiseState(SharpClient.Core.Connection.ConnectionState.Connected);
        conn.Emit("<a xch_cmd=\"go north\">North</a>");

        await Assert.That(session.Scrollback[^1].Segments.All(s => s.Command is null)).IsTrue();
    }
}
