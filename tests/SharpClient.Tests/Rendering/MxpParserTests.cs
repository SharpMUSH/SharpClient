using SharpClient.Core.Rendering;

namespace SharpClient.Tests.Rendering;

public sealed class MxpParserTests
{
    private static MxpParserState Mxp() => new() { IsMxpActive = true };

    [Test]
    public async Task SendOnSecureLineProducesCommandLink()
    {
        var segments = AnsiParser.Parse("\e[1z<SEND href=\"go north\" hint=\"Move north\">North</SEND>", Mxp());

        await Assert.That(segments.Count).IsEqualTo(1);
        await Assert.That(segments[0].Text).IsEqualTo("North");
        await Assert.That(segments[0].Command).IsEqualTo("go north");
        await Assert.That(segments[0].Hint).IsEqualTo("Move north");
    }

    [Test]
    public async Task ShorthandSendUsesInnerTextAsCommand()
    {
        var segments = AnsiParser.Parse("\e[1z<SEND>look</SEND>", Mxp());

        await Assert.That(segments.Count).IsEqualTo(1);
        await Assert.That(segments[0].Text).IsEqualTo("look");
        await Assert.That(segments[0].Command).IsEqualTo("look");
    }

    [Test]
    public async Task SendOnOpenLineIsStrippedWithNoCommand()
    {
        // No ESC[1z: line stays in Open mode, where Secure elements are forbidden.
        var segments = AnsiParser.Parse("<SEND href=\"go north\">North</SEND>", Mxp());

        await Assert.That(segments.Count).IsEqualTo(1);
        await Assert.That(segments[0].Text).IsEqualTo("North"); // inner text shown as plain
        await Assert.That(segments[0].Command).IsNull();
    }

    [Test]
    public async Task LockSecurePersistsAcrossLines()
    {
        var mxp = Mxp();

        // Lock-Secure makes Secure the persistent default.
        _ = AnsiParser.Parse("\e[6zwelcome", mxp);
        mxp.BeginLine(); // simulate next line start

        var segments = AnsiParser.Parse("<SEND href=\"quit\">Quit</SEND>", mxp);

        await Assert.That(segments[0].Command).IsEqualTo("quit");
    }

    [Test]
    public async Task LockedModeTreatsAngleBracketsAsLiteralText()
    {
        var segments = AnsiParser.Parse("\e[2za <b> c", Mxp());

        await Assert.That(segments.Count).IsEqualTo(1);
        await Assert.That(segments[0].Text).IsEqualTo("a <b> c");
        await Assert.That(segments[0].Command).IsNull();
    }

    [Test]
    public async Task PuebloAnchorWithXchCmdProducesCommandLink()
    {
        var mxp = new MxpParserState { IsPuebloActive = true };

        var segments = AnsiParser.Parse("<a xch_cmd=\"go north\" xch_hint=\"Move\">North</a>", mxp);

        await Assert.That(segments.Count).IsEqualTo(1);
        await Assert.That(segments[0].Text).IsEqualTo("North");
        await Assert.That(segments[0].Command).IsEqualTo("go north");
        await Assert.That(segments[0].Hint).IsEqualTo("Move");
    }

    [Test]
    public async Task TagsAreInertWhenNeitherMxpNorPuebloActive()
    {
        // Default Parse (no MXP state) must behave exactly as before — '<' is literal.
        var segments = AnsiParser.Parse("<SEND href=\"x\">y</SEND>");

        await Assert.That(segments.Count).IsEqualTo(1);
        await Assert.That(segments[0].Text).IsEqualTo("<SEND href=\"x\">y</SEND>");
        await Assert.That(segments[0].Command).IsNull();
    }

    [Test]
    public async Task LinkKeepsCommandAcrossSgrChangeInside()
    {
        var segments = AnsiParser.Parse("\e[1z<SEND href=\"hit\">\e[31mAttack\e[0m</SEND>", Mxp());

        await Assert.That(segments.Count).IsEqualTo(1);
        await Assert.That(segments[0].Text).IsEqualTo("Attack");
        await Assert.That(segments[0].Command).IsEqualTo("hit");
        await Assert.That(segments[0].Style.Foreground).IsEqualTo(AnsiColor.Indexed(1));
    }
}
