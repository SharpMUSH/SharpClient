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
    public async Task PuebloBannerTriggersHandshakeAndEnablesLinks()
    {
        var conn = new FakeTelnetConnection();
        await using var session = new Session(conn);

        // The real-world banner format (latest released Pueblo).
        conn.Emit("This world is Pueblo 1.10 Enhanced.");

        await Assert.That(conn.Sent).Contains("PUEBLOCLIENT 2.01");

        conn.Emit("<a xch_cmd=\"go north\">North</a>");

        var line = session.Scrollback[^1];
        await Assert.That(line.Segments[0].Text).IsEqualTo("North");
        await Assert.That(line.Segments[0].Command).IsEqualTo("go north");
    }
}
