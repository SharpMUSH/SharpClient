using SharpClient.Core.Rendering;

namespace SharpClient.Tests.Rendering;

public sealed class InlineImageTests
{
    private static MxpParserState Mxp() => new() { IsMxpActive = true };

    private static MxpParserState Pueblo() => new() { IsPuebloActive = true };

    [Test]
    public async Task MxpImageOnSecureLineJoinsUrlTypeAndName()
    {
        var segments = AnsiParser.Parse("\e[1z<IMAGE map.png URL=\"https://h.example/img/\" T=maps>", Mxp());

        await Assert.That(segments.Count).IsEqualTo(1);
        await Assert.That(segments[0].ImageUrl).IsEqualTo("https://h.example/img/maps/map.png");
        await Assert.That(segments[0].Text).IsEqualTo("map.png");
    }

    [Test]
    public async Task MxpImageAcceptsFnameAttribute()
    {
        var segments = AnsiParser.Parse("\e[1z<IMAGE FName=\"https://h.example/a/b.jpg?x=1\">", Mxp());

        await Assert.That(segments[0].ImageUrl).IsEqualTo("https://h.example/a/b.jpg?x=1");
        await Assert.That(segments[0].Text).IsEqualTo("b.jpg");
    }

    [Test]
    public async Task MxpImageOnOpenLineIsIgnored()
    {
        var segments = AnsiParser.Parse("before <IMAGE https://h.example/x.png> after", Mxp());

        await Assert.That(segments.Any(s => s.ImageUrl is not null)).IsFalse();
    }

    [Test]
    public async Task MxpImageWithBareFileNameAndNoUrlIsIgnored()
    {
        var segments = AnsiParser.Parse("\e[1z<IMAGE map.png>", Mxp());

        await Assert.That(segments.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ImagesAreIgnoredWithoutMxpOrPueblo()
    {
        var segments = AnsiParser.Parse("\e[1z<IMAGE https://h.example/x.png><img src=\"https://h.example/y.png\">", new MxpParserState());

        await Assert.That(segments.Any(s => s.ImageUrl is not null)).IsFalse();
    }

    [Test]
    public async Task PuebloImgUsesSrcAndAlt()
    {
        var segments = AnsiParser.Parse("Look: <img src=\"https://h.example/cat.gif\" alt=\"a cat\">!", Pueblo());

        await Assert.That(segments.Count).IsEqualTo(3);
        await Assert.That(segments[1].ImageUrl).IsEqualTo("https://h.example/cat.gif");
        await Assert.That(segments[1].Text).IsEqualTo("a cat");
        await Assert.That(segments[2].Text).IsEqualTo("!");
    }

    [Test]
    public async Task PuebloModeSwitchImgIsNotAPicture()
    {
        var segments = AnsiParser.Parse("</xch_mudtext><img xch_mode=purehtml><xch_page clear=text>", Pueblo());

        await Assert.That(segments.Count).IsEqualTo(0);
    }

    [Test]
    [Arguments("javascript:alert(1)")]
    [Arguments("file:///etc/passwd")]
    [Arguments("/local/path.png")]
    [Arguments("content://media/1")]
    public async Task NonWebSchemesAreRefused(string src)
    {
        var segments = AnsiParser.Parse($"<img src=\"{src}\">", Pueblo());

        await Assert.That(segments.Count).IsEqualTo(0);
    }

    [Test]
    public async Task DataImageIsAccepted()
    {
        var segments = AnsiParser.Parse("<img src=\"data:image/png;base64,iVBORw0KGgo=\">", Pueblo());

        await Assert.That(segments[0].ImageUrl).IsEqualTo("data:image/png;base64,iVBORw0KGgo=");
    }

    [Test]
    public async Task ImageInsideASendLinkIsClickable()
    {
        var segments = AnsiParser.Parse("\e[1z<SEND href=\"look map\"><IMAGE https://h.example/m.png></SEND>", Mxp());

        await Assert.That(segments[0].ImageUrl).IsEqualTo("https://h.example/m.png");
        await Assert.That(segments[0].Command).IsEqualTo("look map");
    }
}
