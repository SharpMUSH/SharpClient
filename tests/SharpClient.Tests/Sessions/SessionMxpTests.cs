using SharpClient.Core.Sessions;
using SharpClient.Tests.Rendering;

namespace SharpClient.Tests.Sessions;

public sealed class SessionMxpTests
{
    // What a Pueblo server sends once the client has answered PUEBLOCLIENT: leave MUD text for HTML.
    private const string PuebloHtml = "</xch_mudtext><img xch_mode=html>";

    [Test]
    public async Task MxpSecureSendBecomesClickableSegment()
    {
        var conn = new FakeTelnetConnection();
        await using var session = new Session(conn);

        conn.EnableMxp();
        conn.Emit("\e[1z<SEND href=\"look\">Look here</SEND>");

        var link = session.Scrollback[^1].Dom().QuerySelector("a[xch_cmd]")!;
        await Assert.That(link.TextContent).IsEqualTo("Look here");
        await Assert.That(link.GetAttribute("xch_cmd")).IsEqualTo("look");
    }

    [Test]
    public async Task SendIsNotClickableWhenMxpNeverNegotiated()
    {
        var conn = new FakeTelnetConnection();
        await using var session = new Session(conn);

        // No EnableMxp(): markup must pass through inert, never clickable.
        conn.Emit("\e[1z<SEND href=\"rm -rf\">click</SEND>");

        await Assert.That(session.Scrollback[^1].Dom().Commands()).IsEmpty();
    }

    [Test]
    public async Task PuebloModeFromTheServerEnablesLinks()
    {
        var conn = new FakeTelnetConnection();
        await using var session = new Session(conn);

        conn.EnablePueblo();
        conn.Emit(PuebloHtml);
        conn.Emit("<a xch_cmd=\"go north\">North</a>");

        var link = session.Scrollback[^1].Dom().QuerySelector("a[xch_cmd]")!;
        await Assert.That(link.TextContent).IsEqualTo("North");
        await Assert.That(link.GetAttribute("xch_cmd")).IsEqualTo("go north");
    }

    [Test]
    public async Task QuotedPuebloHelloDoesNotEnablePueblo()
    {
        var conn = new FakeTelnetConnection();
        await using var session = new Session(conn);

        // A player can say the hello. Only the server's start sequence (via TNC) enables Pueblo, so
        // the anchor that follows must stay inert and nothing may be sent.
        conn.Emit("Bob says, \"This world is Pueblo 1.10 Enhanced.\"");
        conn.Emit(PuebloHtml);
        conn.Emit("<a xch_cmd=\"@destroy me\">free stuff</a>");

        await Assert.That(conn.Sent).IsEmpty();
        await Assert.That(session.Scrollback[^1].Dom().Commands()).IsEmpty();
    }

    [Test]
    public async Task MarkupStateIsDroppedWhenTheConnectionGoesDown()
    {
        var conn = new FakeTelnetConnection();
        await using var session = new Session(conn);

        conn.RaiseState(SharpClient.Core.Connection.ConnectionState.Connected);
        conn.EnablePueblo();
        conn.Emit(PuebloHtml);
        conn.RaiseState(SharpClient.Core.Connection.ConnectionState.Reconnecting);
        conn.RaiseState(SharpClient.Core.Connection.ConnectionState.Connected);
        conn.Emit(PuebloHtml);
        conn.Emit("<a xch_cmd=\"go north\">North</a>");

        await Assert.That(session.Scrollback[^1].Dom().Commands()).IsEmpty();
    }
}
