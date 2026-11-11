using AngleSharp.Dom;
using SharpClient.Core.Rendering;
using static SharpClient.Tests.Rendering.LineHtml;

namespace SharpClient.Tests.Rendering;

/// <summary>
/// What a line from the server looks like once decoded and written as the output view's HTML: every
/// capability the old AnsiParser had, and the SGR it used to drop.
/// </summary>
public sealed class OutputRenderingTests
{
    private const string PuebloHtml = "</xch_mudtext><img xch_mode=html>";

    private static IElement Plain(string raw) => Render(new OutputDecoder(), raw);

    private static IElement Mxp(string raw)
    {
        var decoder = new OutputDecoder();
        decoder.EnableMxp();
        return Render(decoder, raw);
    }

    private static IElement Pueblo(string raw, bool showImages = true)
    {
        var decoder = new OutputDecoder();
        decoder.EnablePueblo();
        decoder.Decode(PuebloHtml);
        return Render(decoder, raw, showImages);
    }

    private static IElement Render(OutputDecoder decoder, string raw, bool showImages = true) =>
        decoder.Decode(raw).Single().Dom(showImages);

    /// <summary>The innermost element around <paramref name="text"/>.</summary>
    private static IElement At(IElement root, string text) =>
        root.Descendants<IElement>().Last(e => e.TextContent == text);

    // ── ANSI ──────────────────────────────────────────────────────────────

    [Test]
    public async Task PlainTextIsJustText()
    {
        var line = Plain("hello world");

        await Assert.That(line.InnerHtml).IsEqualTo("hello world");
    }

    [Test]
    public async Task TextIsEscaped()
    {
        await Assert.That(Plain("<b>x</b> & y").InnerHtml).IsEqualTo("&lt;b&gt;x&lt;/b&gt; &amp; y");
    }

    [Test]
    public async Task EmptyLineIsEmpty()
    {
        await Assert.That(Plain(string.Empty).InnerHtml).IsEmpty();
    }

    [Test]
    [Arguments("\e[31m", 1)]
    [Arguments("\e[92m", 10)]
    [Arguments("\e[38;5;208m", 208)]
    [Arguments("\e[38;5;3m", 3)]
    [Arguments("\e[38:5:208m", 208)]
    public async Task PaletteColoursUseTheClientPalette(string sgr, int index)
    {
        await Assert.That(At(Plain(sgr + "x"), "x").Color()).IsEqualTo(Hex(index));
    }

    [Test]
    public async Task StandardColoursAreTheClientsOwn()
    {
        await Assert.That(OutputPalette.CustomProperties).Contains("--ms-ansi-1:#e06c75");
        await Assert.That(OutputPalette.CustomProperties).Contains("--ms-ansi-15:#e8edf2");
        await Assert.That(At(Plain("\e[31mx"), "x").Color()).IsEqualTo("#e06c75");
        await Assert.That(At(Plain("\e[97mx"), "x").Color()).IsEqualTo("#e8edf2");
        await Assert.That(At(Plain("\e[44mx"), "x").BackgroundColor()).IsEqualTo("#61afef");
        await Assert.That(At(Plain("\e[38;5;16mx"), "x").Color()).IsEqualTo("#000000");
        await Assert.That(At(Plain("\e[38;5;232mx"), "x").Color()).IsEqualTo("#080808");
    }

    [Test]
    public async Task ResetEndsTheColour()
    {
        var line = Plain("\e[31mred\e[0m normal");

        await Assert.That(At(line, "red").Color()).IsEqualTo(Hex(1));
        await Assert.That(line.LastChild!.TextContent).IsEqualTo(" normal");
        await Assert.That(line.LastChild).IsTypeOf<AngleSharp.Dom.IText>();
    }

    [Test]
    public async Task StyleCarriesToTheNextLine()
    {
        var decoder = new OutputDecoder();
        decoder.Decode("\e[32mgreen");

        await Assert.That(At(Render(decoder, "still green"), "still green").Color()).IsEqualTo(Hex(2));
    }

    [Test]
    public async Task TruecolorForegroundAndBackground()
    {
        var fg = At(Plain("\e[38;2;40;50;60mX"), "X");
        var bg = At(Plain("\e[48;2;10;20;30mX"), "X");

        await Assert.That(fg.Color()).IsEqualTo("#28323c");
        await Assert.That(fg.BackgroundColor()).IsNull();
        await Assert.That(bg.BackgroundColor()).IsEqualTo("#0a141e");
        await Assert.That(bg.Color()).IsNull();
    }

    [Test]
    public async Task TruecolorAmongOtherCodesAppliesAll()
    {
        var span = At(Plain("\e[1;38;2;255;128;0;4mX"), "X");

        await Assert.That(span.Color()).IsEqualTo("#ff8000");
        await Assert.That(span.ClassList).Contains("ms-bold");
        await Assert.That(span.ClassList).Contains("ms-underline");
    }

    [Test]
    public async Task TruncatedTruecolorColoursNothing()
    {
        // "38;2;40" is missing G and B: nothing is coloured, and "40" is not read as a background.
        await Assert.That(Plain("\e[38;2;40mX").InnerHtml).IsEqualTo("X");
    }

    [Test]
    [Arguments("\e[1m", "ms-bold")]
    [Arguments("\e[2m", "ms-faint")]
    [Arguments("\e[3m", "ms-italic")]
    [Arguments("\e[4m", "ms-underline")]
    [Arguments("\e[21m", "ms-underline")]
    [Arguments("\e[5m", "ms-blink")]
    [Arguments("\e[8m", "ms-conceal")]
    [Arguments("\e[9m", "ms-strike")]
    [Arguments("\e[53m", "ms-overline")]
    public async Task AttributesAreDrawn(string sgr, string cssClass)
    {
        await Assert.That(At(Plain(sgr + "x"), "x").ClassList).Contains(cssClass);
    }

    [Test]
    [Arguments("\e[1m", "\e[22m")]
    [Arguments("\e[3m", "\e[23m")]
    [Arguments("\e[4m", "\e[24m")]
    [Arguments("\e[5m", "\e[25m")]
    [Arguments("\e[7m", "\e[27m")]
    [Arguments("\e[9m", "\e[29m")]
    public async Task AttributesTurnOff(string on, string off)
    {
        var line = Plain(on + "a" + off + "b");

        await Assert.That(line.LastChild!.TextContent).IsEqualTo("b");
        await Assert.That(line.LastChild).IsTypeOf<AngleSharp.Dom.IText>();
    }

    [Test]
    public async Task InverseSwapsColoursAgainstTheOutputDefaults()
    {
        var coloured = At(Plain("\e[32;7mx"), "x");
        var background = At(Plain("\e[41;7mx"), "x");
        var plain = At(Plain("\e[7mx"), "x");

        await Assert.That(coloured.Color()).IsEqualTo("#090c10");
        await Assert.That(coloured.BackgroundColor()).IsEqualTo(Hex(2));
        await Assert.That(background.Color()).IsEqualTo(Hex(1));
        await Assert.That(background.BackgroundColor()).IsEqualTo("#c4d1c8");
        // No colour to swap: ms-invert, which the stylesheet draws with --ms-bg on --ms-fg.
        await Assert.That(plain.ClassList).Contains("ms-invert");
    }

    [Test]
    public async Task BoldIsNotBrightened()
    {
        await Assert.That(At(Plain("\e[1;31mx"), "x").Color()).IsEqualTo(Hex(1));
    }

    [Test]
    public async Task OtherControlSequencesAreNotShown()
    {
        await Assert.That(Plain("a\e[?25lb\e[Hc").TextContent).IsEqualTo("abc");
    }

    [Test]
    public async Task OscHyperlinkIsAWebLink()
    {
        var line = Plain("see \e]8;;https://example.com/help\e\\help\e]8;;\e\\ now");

        var link = line.QuerySelector("a")!;
        await Assert.That(link.TextContent).IsEqualTo("help");
        await Assert.That(link.GetAttribute("href")).IsEqualTo("https://example.com/help");
        await Assert.That(link.GetAttribute("target")).IsEqualTo("_blank");
        await Assert.That(link.GetAttribute("rel")).IsEqualTo("noopener noreferrer");
        await Assert.That(line.QuerySelectorAll("a").Length).IsEqualTo(1);
    }

    [Test]
    public async Task OscHyperlinkToAScriptIsText()
    {
        var line = Plain("\e]8;;javascript:alert(1)\e\\x\e]8;;\e\\");

        await Assert.That(line.InnerHtml).IsEqualTo("x");
    }

    [Test]
    public async Task MarkupIsLiteralWithoutMxpOrPueblo()
    {
        var line = Plain("<SEND href=\"x\">y</SEND> 5 &gt; 3");

        await Assert.That(line.TextContent).IsEqualTo("<SEND href=\"x\">y</SEND> 5 &gt; 3");
        await Assert.That(line.Commands()).IsEmpty();
    }

    // ── MXP ───────────────────────────────────────────────────────────────

    [Test]
    public async Task SendOnSecureLineIsACommandLink()
    {
        var line = Mxp("\e[1z<SEND href=\"go north\" hint=\"Move north\">North</SEND>");

        var link = line.QuerySelector("a.ms-cmd-link")!;
        await Assert.That(link.TextContent).IsEqualTo("North");
        await Assert.That(link.GetAttribute("xch_cmd")).IsEqualTo("go north");
        await Assert.That(link.GetAttribute("title")).IsEqualTo("Move north");
        await Assert.That(line.TextContent).IsEqualTo("North");
    }

    [Test]
    public async Task ShorthandSendSendsItsText()
    {
        var line = Mxp("\e[1z<SEND>look</SEND>");

        await Assert.That(line.Commands()).IsEquivalentTo(["look"]);
    }

    [Test]
    public async Task SendOnOpenLineShowsItsTextOnly()
    {
        var line = Mxp("<SEND href=\"go north\">North</SEND>");

        await Assert.That(line.InnerHtml).IsEqualTo("North");
    }

    [Test]
    public async Task LockSecurePersistsAcrossLines()
    {
        var decoder = new OutputDecoder();
        decoder.EnableMxp();
        decoder.Decode("\e[6zwelcome");

        await Assert.That(Render(decoder, "<SEND href=\"quit\">Quit</SEND>").Commands()).IsEquivalentTo(["quit"]);
    }

    [Test]
    public async Task SecureLineModeEndsWithTheLine()
    {
        var decoder = new OutputDecoder();
        decoder.EnableMxp();
        decoder.Decode("\e[1zsecure");

        await Assert.That(Render(decoder, "<SEND href=\"quit\">Quit</SEND>").Commands()).IsEmpty();
    }

    [Test]
    public async Task SecureLineEndingInBreakEndsWithTheTelnetLine()
    {
        var decoder = new OutputDecoder();
        decoder.EnableMxp();
        decoder.Decode("\e[1zserver<BR>");

        await Assert.That(Render(decoder, "<SEND href=\"quit\">Quit</SEND>").Commands()).IsEmpty();
    }

    [Test]
    public async Task LockedLineShowsMarkupAsText()
    {
        await Assert.That(Mxp("\e[2za <b> c").TextContent).IsEqualTo("a <b> c");
    }

    [Test]
    public async Task ColourInsideALinkKeepsTheLink()
    {
        var line = Mxp("\e[1z<SEND href=\"hit\">\e[31mAttack\e[0m</SEND>");

        var link = line.QuerySelector("a.ms-cmd-link")!;
        await Assert.That(link.TextContent).IsEqualTo("Attack");
        await Assert.That(link.GetAttribute("xch_cmd")).IsEqualTo("hit");
        await Assert.That(link.Color()).IsEqualTo(Hex(1));
    }

    [Test]
    public async Task SendWithSeveralCommandsIsAMenu()
    {
        var line = Mxp("\e[1z<SEND href=\"buy sword|look sword\" hint=\"Sword|Buy|Look\">sword</SEND>");

        var menu = line.QuerySelector(".ms-menu")!;
        await Assert.That(menu.TextContent).IsEqualTo("sword");
        await Assert.That(menu.GetAttribute("title")).IsEqualTo("Sword");
        await Assert.That(menu.GetAttribute("data-commands"))
            .IsEqualTo("[{\"label\":\"Buy\",\"command\":\"buy sword\"},{\"label\":\"Look\",\"command\":\"look sword\"}]");
    }

    [Test]
    public async Task PromptSendIsAPromptMenu()
    {
        var line = Mxp("\e[1z<SEND href=\"say \" PROMPT>talk</SEND>");

        // markupstring.js asks the view's Prompt, which sends it.
        await Assert.That(line.QuerySelector(".ms-menu.ms-prompt")!.TextContent).IsEqualTo("talk");
    }

    [Test]
    public async Task EntitiesAreDecoded()
    {
        await Assert.That(Mxp("5 &gt; 3 &amp; &lt;").TextContent).IsEqualTo("5 > 3 & <");
    }

    [Test]
    public async Task OpenTagsStyleText()
    {
        var line = Mxp("<B>bold</B> <I>it</I> <U>u</U> <S>s</S> <COLOR fore=red back=#000080>c</COLOR>");

        await Assert.That(At(line, "bold").ClassList).Contains("ms-bold");
        await Assert.That(At(line, "it").ClassList).Contains("ms-italic");
        await Assert.That(At(line, "u").ClassList).Contains("ms-underline");
        await Assert.That(At(line, "s").ClassList).Contains("ms-strike");
        await Assert.That(At(line, "c").Color()).IsEqualTo("#ff0000");
        await Assert.That(At(line, "c").BackgroundColor()).IsEqualTo("#000080");
    }

    [Test]
    public async Task MxpAnchorIsAWebLink()
    {
        var link = Mxp("\e[1z<A href=\"https://example.com\" hint=\"Site\">site</A>").QuerySelector("a")!;

        await Assert.That(link.GetAttribute("href")).IsEqualTo("https://example.com");
        await Assert.That(link.GetAttribute("title")).IsEqualTo("Site");
    }

    [Test]
    public async Task BreakStartsANewLine()
    {
        var decoder = new OutputDecoder();
        decoder.EnableMxp();

        var lines = decoder.Decode("one<BR>two");

        await Assert.That(lines.Select(l => l.Text)).IsEquivalentTo(["one", "two"]);
    }

    [Test]
    public async Task MxpImageOnSecureLineIsAPicture()
    {
        var image = Mxp("\e[1z<IMAGE FName=\"https://h.example/a/b.jpg?x=1\">").QuerySelector("img")!;

        await Assert.That(image.GetAttribute("src")).IsEqualTo("https://h.example/a/b.jpg?x=1");
        await Assert.That(image.GetAttribute("alt")).IsEqualTo("b.jpg");
        await Assert.That(image.GetAttribute("referrerpolicy")).IsEqualTo("no-referrer");
        await Assert.That(image.GetAttribute("loading")).IsEqualTo("lazy");
    }

    [Test]
    public async Task MxpImageJoinsUrlAndName()
    {
        var image = Mxp("\e[1z<IMAGE map.png URL=\"https://h.example/img/\">").QuerySelector("img")!;

        await Assert.That(image.GetAttribute("src")).IsEqualTo("https://h.example/img/map.png");
    }

    [Test]
    public async Task MxpImageJoinsUrlTypeAndName()
    {
        var image = Mxp("\e[1z<IMAGE map.png URL=\"https://h.example/img/\" T=maps>").QuerySelector("img")!;

        await Assert.That(image.GetAttribute("src")).IsEqualTo("https://h.example/img/maps/map.png");
        await Assert.That(image.GetAttribute("alt")).IsEqualTo("map.png");
    }

    [Test]
    public async Task MxpImageSizeIsKept()
    {
        var image = Mxp("\e[1z<IMAGE https://h.example/x.png W=64 H=32>").QuerySelector("img")!;

        await Assert.That(image.GetAttribute("width")).IsEqualTo("64");
        await Assert.That(image.GetAttribute("height")).IsEqualTo("32");
    }

    [Test]
    public async Task MxpImageOnOpenLineIsNotAPicture()
    {
        await Assert.That(Mxp("before <IMAGE https://h.example/x.png> after").QuerySelector("img")).IsNull();
    }

    [Test]
    public async Task MxpImageWithBareFileNameShowsNothing()
    {
        await Assert.That(Mxp("\e[1z<IMAGE map.png>").InnerHtml).IsEmpty();
    }

    [Test]
    public async Task ImageInsideASendIsClickable()
    {
        var link = Mxp("\e[1z<SEND href=\"look map\"><IMAGE https://h.example/m.png></SEND>").QuerySelector("a.ms-cmd-link")!;

        await Assert.That(link.GetAttribute("xch_cmd")).IsEqualTo("look map");
        await Assert.That(link.QuerySelector("img")!.GetAttribute("src")).IsEqualTo("https://h.example/m.png");
    }

    [Test]
    public async Task ImagesAreTextWithoutMxpOrPueblo()
    {
        await Assert.That(Plain("\e[1z<IMAGE https://h.example/x.png><img src=\"https://h.example/y.png\">").QuerySelector("img")).IsNull();
    }

    [Test]
    public async Task CustomElementsExpand()
    {
        var decoder = new OutputDecoder();
        decoder.EnableMxp();
        decoder.Decode("\e[1z<!ELEMENT RName '<B><COLOR red>'>");

        var hall = At(Render(decoder, "\e[1z<RName>The Hall</RName>"), "The Hall");

        await Assert.That(hall.ClassList).Contains("ms-bold");
        await Assert.That(hall.Color()).IsEqualTo("#ff0000");
    }

    // ── Pueblo ────────────────────────────────────────────────────────────

    [Test]
    public async Task PuebloAnchorWithXchCmdIsACommandLink()
    {
        var link = Pueblo("<a xch_cmd=\"go north\" xch_hint=\"Move\">North</a>").QuerySelector("a.ms-cmd-link")!;

        await Assert.That(link.TextContent).IsEqualTo("North");
        await Assert.That(link.GetAttribute("xch_cmd")).IsEqualTo("go north");
        await Assert.That(link.GetAttribute("title")).IsEqualTo("Move");
    }

    [Test]
    public async Task PuebloEntitiesAreDecoded()
    {
        await Assert.That(Pueblo("5 &gt; 3 &amp;&amp; 2 &lt; 4 &quot;q&quot;").TextContent).IsEqualTo("5 > 3 && 2 < 4 \"q\"");
        await Assert.That(Pueblo("it&apos;s&nbsp;here").TextContent).IsEqualTo("it's here");
        await Assert.That(Pueblo("&#62;&#x3E;&#38;").TextContent).IsEqualTo(">>&");
    }

    [Test]
    public async Task LoneAndUnknownAmpersandsStay()
    {
        await Assert.That(Pueblo("Tom & Jerry").TextContent).IsEqualTo("Tom & Jerry");
        await Assert.That(Pueblo("x&notathing;y").TextContent).IsEqualTo("x&notathing;y");
    }

    [Test]
    public async Task EntityInsideAnAttributeIsDecoded()
    {
        await Assert.That(Pueblo("<a xch_cmd=\"say 5 &gt; 3\">go</a>").Commands()).IsEquivalentTo(["say 5 > 3"]);
    }

    [Test]
    public async Task GreaterThanInsideAQuotedAttributeDoesNotEndTheTag()
    {
        var line = Pueblo("<a xch_cmd=\"say 5 > 3\">go</a>");

        await Assert.That(line.TextContent).IsEqualTo("go");
        await Assert.That(line.Commands()).IsEquivalentTo(["say 5 > 3"]);
    }

    [Test]
    public async Task PuebloImgUsesSrcAndAlt()
    {
        var line = Pueblo("Look: <img src=\"https://h.example/cat.gif\" alt=\"a cat\">!");

        var image = line.QuerySelector("img")!;
        await Assert.That(image.GetAttribute("src")).IsEqualTo("https://h.example/cat.gif");
        await Assert.That(image.GetAttribute("alt")).IsEqualTo("a cat");
        await Assert.That(line.TextContent).IsEqualTo("Look: !");
    }

    [Test]
    public async Task ImagesOffShowsTheirName()
    {
        var line = Pueblo("Look: <img src=\"https://h.example/cat.gif\" alt=\"a cat\">!", showImages: false);

        await Assert.That(line.QuerySelector("img")).IsNull();
        await Assert.That(line.TextContent).IsEqualTo("Look: a cat!");
    }

    [Test]
    public async Task PuebloModeSwitchesAndPageClearShowNothing()
    {
        var line = Pueblo("</xch_mudtext><img xch_mode=purehtml><xch_page clear=text>");

        await Assert.That(line.TextContent).IsEmpty();
        await Assert.That(line.QuerySelector("img")).IsNull();
    }

    [Test]
    [Arguments("javascript:alert(1)")]
    [Arguments("file:///etc/passwd")]
    [Arguments("/local/path.png")]
    [Arguments("content://media/1")]
    public async Task NonWebImageSourcesShowNothing(string src)
    {
        await Assert.That(Pueblo($"<img src=\"{src}\">").InnerHtml).IsEmpty();
        await Assert.That(Pueblo($"<img src=\"{src}\">", showImages: false).InnerHtml).IsEmpty();
    }

    [Test]
    public async Task DataImageIsAccepted()
    {
        var image = Pueblo("<img src=\"data:image/png;base64,iVBORw0KGgo=\">").QuerySelector("img")!;

        await Assert.That(image.GetAttribute("src")).IsEqualTo("data:image/png;base64,iVBORw0KGgo=");
    }

    [Test]
    public async Task PuebloLineEndingInBreakAddsNoBlankLine()
    {
        var decoder = new OutputDecoder();
        decoder.EnablePueblo();
        decoder.Decode(PuebloHtml);

        var lines = decoder.Decode("Hello<br>");

        await Assert.That(lines.Select(l => l.Text)).IsEquivalentTo(["Hello"]);
    }

    [Test]
    public async Task ResetForgetsProtocolsAndStyle()
    {
        var decoder = new OutputDecoder();
        decoder.EnableMxp();
        decoder.Decode("\e[31m\e[6z");
        decoder.Reset();

        var line = Render(decoder, "<SEND href=\"x\">y</SEND>");

        await Assert.That(decoder.ActiveProtocol).IsNull();
        await Assert.That(line.TextContent).IsEqualTo("<SEND href=\"x\">y</SEND>");
        await Assert.That(line.Children.Length).IsEqualTo(0);
    }
}
