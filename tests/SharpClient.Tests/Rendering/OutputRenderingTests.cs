using MarkupString.Display;
using SharpClient.Core.Rendering;

namespace SharpClient.Tests.Rendering;

/// <summary>
/// What a line from the server looks like once decoded and folded into spans: every capability the
/// old AnsiParser had, and the SGR it used to drop.
/// </summary>
public sealed class OutputRenderingTests
{
    private const string PuebloHtml = "</xch_mudtext><img xch_mode=html>";

    private static IReadOnlyList<DisplaySpan> Plain(string raw) => Render(new OutputDecoder(), raw);

    private static IReadOnlyList<DisplaySpan> Mxp(string raw)
    {
        var decoder = new OutputDecoder();
        decoder.EnableMxp();
        return Render(decoder, raw);
    }

    private static IReadOnlyList<DisplaySpan> Pueblo(string raw)
    {
        var decoder = new OutputDecoder();
        decoder.EnablePueblo();
        decoder.Decode(PuebloHtml);
        return Render(decoder, raw);
    }

    private static IReadOnlyList<DisplaySpan> Render(OutputDecoder decoder, string raw) =>
        [.. decoder.Decode(raw).SelectMany(line => line.Spans())];

    private static string Text(IReadOnlyList<DisplaySpan> spans) => string.Concat(spans.Select(s => s.Text));

    private static string Hex(int index) => SpanRenderer.Hex(OutputPalette.Instance.Entry(index));

    // ── ANSI ──────────────────────────────────────────────────────────────

    [Test]
    public async Task PlainTextIsOneUnstyledSpan()
    {
        var spans = Plain("hello world");

        await Assert.That(spans.Count).IsEqualTo(1);
        await Assert.That(spans[0].Text).IsEqualTo("hello world");
        await Assert.That(spans[0].Style).IsEqualTo(DisplayStyle.Plain);
    }

    [Test]
    public async Task EmptyLineHasNoSpans()
    {
        await Assert.That(Plain(string.Empty)).IsEmpty();
    }

    [Test]
    [Arguments("\e[31m", 1)]
    [Arguments("\e[92m", 10)]
    [Arguments("\e[38;5;208m", 208)]
    [Arguments("\e[38;5;3m", 3)]
    [Arguments("\e[38:5:208m", 208)]
    public async Task PaletteColoursUseTheClientPalette(string sgr, int index)
    {
        var spans = Plain(sgr + "x");

        await Assert.That(spans[0].Color()).IsEqualTo(Hex(index));
    }

    [Test]
    public async Task StandardColoursAreTheClientsOwn()
    {
        await Assert.That(Plain("\e[31mx")[0].Color()).IsEqualTo("#e06c75");
        await Assert.That(Plain("\e[97mx")[0].Color()).IsEqualTo("#e8edf2");
        await Assert.That(Plain("\e[44mx")[0].BackgroundColor()).IsEqualTo("#61afef");
        await Assert.That(Plain("\e[38;5;16mx")[0].Color()).IsEqualTo("#000000");
        await Assert.That(Plain("\e[38;5;232mx")[0].Color()).IsEqualTo("#080808");
    }

    [Test]
    public async Task ResetEndsTheColour()
    {
        var spans = Plain("\e[31mred\e[0m normal");

        await Assert.That(spans.Count).IsEqualTo(2);
        await Assert.That(spans[0].Text).IsEqualTo("red");
        await Assert.That(spans[0].Color()).IsEqualTo(Hex(1));
        await Assert.That(spans[1].Text).IsEqualTo(" normal");
        await Assert.That(spans[1].Color()).IsNull();
    }

    [Test]
    public async Task StyleCarriesToTheNextLine()
    {
        var decoder = new OutputDecoder();
        decoder.Decode("\e[32mgreen");

        await Assert.That(decoder.Decode("still green")[0].Spans()[0].Color()).IsEqualTo(Hex(2));
    }

    [Test]
    public async Task TruecolorForegroundAndBackground()
    {
        var fg = Plain("\e[38;2;40;50;60mX")[0];
        var bg = Plain("\e[48;2;10;20;30mX")[0];

        await Assert.That(fg.Color()).IsEqualTo("#28323c");
        await Assert.That(fg.BackgroundColor()).IsNull();
        await Assert.That(bg.BackgroundColor()).IsEqualTo("#0a141e");
        await Assert.That(bg.Color()).IsNull();
    }

    [Test]
    public async Task TruecolorAmongOtherCodesAppliesAll()
    {
        var span = Plain("\e[1;38;2;255;128;0;4mX")[0];

        await Assert.That(span.Color()).IsEqualTo("#ff8000");
        await Assert.That(span.Style.Has(TextAttributes.Bold)).IsTrue();
        await Assert.That(span.Style.Has(TextAttributes.Underline)).IsTrue();
    }

    [Test]
    public async Task TruncatedTruecolorColoursNothing()
    {
        // "38;2;40" is missing G and B: nothing is coloured, and "40" is not read as a background.
        var span = Plain("\e[38;2;40mX")[0];

        await Assert.That(span.Color()).IsNull();
        await Assert.That(span.BackgroundColor()).IsNull();
    }

    [Test]
    [Arguments("\e[1m", TextAttributes.Bold)]
    [Arguments("\e[2m", TextAttributes.Faint)]
    [Arguments("\e[3m", TextAttributes.Italic)]
    [Arguments("\e[4m", TextAttributes.Underline)]
    [Arguments("\e[21m", TextAttributes.Underline)]
    [Arguments("\e[5m", TextAttributes.Blink)]
    [Arguments("\e[8m", TextAttributes.Conceal)]
    [Arguments("\e[9m", TextAttributes.Strikethrough)]
    [Arguments("\e[53m", TextAttributes.Overline)]
    public async Task AttributesAreDrawn(string sgr, TextAttributes attribute)
    {
        var span = Plain(sgr + "x")[0];

        await Assert.That(span.Style.Has(attribute)).IsTrue();
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
        var spans = Plain(on + "a" + off + "b");

        await Assert.That(spans[^1].Text).IsEqualTo("b");
        await Assert.That(spans[^1].Style).IsEqualTo(DisplayStyle.Plain);
    }

    [Test]
    public async Task CssCarriesEveryAttribute()
    {
        var css = SpanRenderer.Css(Plain("\e[1;2;3;4;9;53mx")[0].Style);

        await Assert.That(css).Contains("font-weight:bold");
        await Assert.That(css).Contains("opacity:0.6");
        await Assert.That(css).Contains("font-style:italic");
        await Assert.That(css).Contains("text-decoration:underline overline line-through");
        await Assert.That(SpanRenderer.CssClasses(Plain("\e[5mx")[0].Style)).IsEqualTo("ms-blink");
    }

    [Test]
    public async Task InverseSwapsColoursAgainstTheOutputDefaults()
    {
        var coloured = Plain("\e[32;7mx")[0];
        var plain = Plain("\e[7mx")[0];

        await Assert.That(coloured.Color()).IsEqualTo("#090c10");
        await Assert.That(coloured.BackgroundColor()).IsEqualTo(Hex(2));
        await Assert.That(plain.Color()).IsEqualTo("#090c10");
        await Assert.That(plain.BackgroundColor()).IsEqualTo("#c4d1c8");
    }

    [Test]
    public async Task BoldIsNotBrightened()
    {
        await Assert.That(Plain("\e[1;31mx")[0].Color()).IsEqualTo(Hex(1));
    }

    [Test]
    public async Task OtherControlSequencesAreNotShown()
    {
        await Assert.That(Text(Plain("a\e[?25lb\e[Hc"))).IsEqualTo("abc");
    }

    [Test]
    public async Task OscHyperlinkIsAWebLink()
    {
        var spans = Plain("see \e]8;;https://example.com/help\e\\help\e]8;;\e\\ now");

        await Assert.That(spans[1].Text).IsEqualTo("help");
        await Assert.That(spans[1].Style.Interaction).IsEqualTo(new Interaction(InteractionKind.Url, "https://example.com/help"));
        await Assert.That(spans[2].Style.Interaction).IsNull();
    }

    [Test]
    public async Task OscHyperlinkToAScriptIsText()
    {
        var spans = Plain("\e]8;;javascript:alert(1)\e\\x\e]8;;\e\\");

        await Assert.That(spans.All(s => s.Style.Interaction is null)).IsTrue();
    }

    [Test]
    public async Task MarkupIsLiteralWithoutMxpOrPueblo()
    {
        var spans = Plain("<SEND href=\"x\">y</SEND> 5 &gt; 3");

        await Assert.That(Text(spans)).IsEqualTo("<SEND href=\"x\">y</SEND> 5 &gt; 3");
        await Assert.That(spans.All(s => s.Command() is null)).IsTrue();
    }

    // ── MXP ───────────────────────────────────────────────────────────────

    [Test]
    public async Task SendOnSecureLineIsACommandLink()
    {
        var spans = Mxp("\e[1z<SEND href=\"go north\" hint=\"Move north\">North</SEND>");

        await Assert.That(spans.Count).IsEqualTo(1);
        await Assert.That(spans[0].Text).IsEqualTo("North");
        await Assert.That(spans[0].Command()).IsEqualTo("go north");
        await Assert.That(spans[0].Style.Interaction!.Hint).IsEqualTo("Move north");
    }

    [Test]
    public async Task ShorthandSendSendsItsText()
    {
        var spans = Mxp("\e[1z<SEND>look</SEND>");

        await Assert.That(spans[0].Text).IsEqualTo("look");
        await Assert.That(spans[0].Command()).IsEqualTo("look");
    }

    [Test]
    public async Task SendOnOpenLineShowsItsTextOnly()
    {
        var spans = Mxp("<SEND href=\"go north\">North</SEND>");

        await Assert.That(Text(spans)).IsEqualTo("North");
        await Assert.That(spans.All(s => s.Command() is null)).IsTrue();
    }

    [Test]
    public async Task LockSecurePersistsAcrossLines()
    {
        var decoder = new OutputDecoder();
        decoder.EnableMxp();
        decoder.Decode("\e[6zwelcome");

        var spans = Render(decoder, "<SEND href=\"quit\">Quit</SEND>");

        await Assert.That(spans[0].Command()).IsEqualTo("quit");
    }

    [Test]
    public async Task SecureLineModeEndsWithTheLine()
    {
        var decoder = new OutputDecoder();
        decoder.EnableMxp();
        decoder.Decode("\e[1zsecure");

        var spans = Render(decoder, "<SEND href=\"quit\">Quit</SEND>");

        await Assert.That(spans.All(s => s.Command() is null)).IsTrue();
    }

    [Test]
    public async Task LockedLineShowsMarkupAsText()
    {
        var spans = Mxp("\e[2za <b> c");

        await Assert.That(Text(spans)).IsEqualTo("a <b> c");
    }

    [Test]
    public async Task ColourInsideALinkKeepsTheLink()
    {
        var spans = Mxp("\e[1z<SEND href=\"hit\">\e[31mAttack\e[0m</SEND>");

        await Assert.That(spans.Count).IsEqualTo(1);
        await Assert.That(spans[0].Text).IsEqualTo("Attack");
        await Assert.That(spans[0].Command()).IsEqualTo("hit");
        await Assert.That(spans[0].Color()).IsEqualTo(Hex(1));
    }

    [Test]
    public async Task SendWithSeveralCommandsIsAMenu()
    {
        var spans = Mxp("\e[1z<SEND href=\"buy sword|look sword\" hint=\"Sword|Buy|Look\">sword</SEND>");

        var interaction = spans[0].Style.Interaction!;
        await Assert.That(interaction.Target).IsEqualTo("buy sword");
        await Assert.That(interaction.Hint).IsEqualTo("Sword");
        await Assert.That(interaction.Choices!.Select(c => (c.Label, c.Command)))
            .IsEquivalentTo([("Buy", "buy sword"), ("Look", "look sword")]);
    }

    [Test]
    public async Task PromptSendIsSentAsACommand()
    {
        var spans = Mxp("\e[1z<SEND href=\"say \" PROMPT>talk</SEND>");

        await Assert.That(spans[0].Style.Interaction!.Kind).IsEqualTo(InteractionKind.Command);
    }

    [Test]
    public async Task EntitiesAreDecoded()
    {
        await Assert.That(Text(Mxp("5 &gt; 3 &amp; &lt;"))).IsEqualTo("5 > 3 & <");
    }

    [Test]
    public async Task OpenTagsStyleText()
    {
        var spans = Mxp("<B>bold</B> <I>it</I> <U>u</U> <S>s</S> <COLOR fore=red back=#000080>c</COLOR>");

        await Assert.That(spans[0].Style.Has(TextAttributes.Bold)).IsTrue();
        await Assert.That(spans[2].Style.Has(TextAttributes.Italic)).IsTrue();
        await Assert.That(spans[4].Style.Has(TextAttributes.Underline)).IsTrue();
        await Assert.That(spans[6].Style.Has(TextAttributes.Strikethrough)).IsTrue();
        await Assert.That(spans[8].Color()).IsEqualTo("#ff0000");
        await Assert.That(spans[8].BackgroundColor()).IsEqualTo("#000080");
    }

    [Test]
    public async Task MxpAnchorIsAWebLink()
    {
        var spans = Mxp("\e[1z<A href=\"https://example.com\" hint=\"Site\">site</A>");

        await Assert.That(spans[0].Style.Interaction).IsEqualTo(new Interaction(InteractionKind.Url, "https://example.com") { Hint = "Site" });
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
        var spans = Mxp("\e[1z<IMAGE FName=\"https://h.example/a/b.jpg?x=1\">");

        await Assert.That(spans[0].Style.Picture!.Source).IsEqualTo("https://h.example/a/b.jpg?x=1");
        await Assert.That(ImageSource.DisplayName(spans[0].Style.Picture!.Source)).IsEqualTo("b.jpg");
    }

    [Test]
    public async Task MxpImageJoinsUrlAndName()
    {
        var spans = Mxp("\e[1z<IMAGE map.png URL=\"https://h.example/img/\">");

        await Assert.That(ImageSource.Resolve(spans[0].Style.Picture!.Source)).IsEqualTo("https://h.example/img/map.png");
    }

    [Test]
    public async Task MxpImageJoinsUrlTypeAndName()
    {
        var spans = Mxp("\e[1z<IMAGE map.png URL=\"https://h.example/img/\" T=maps>");

        await Assert.That(ImageSource.Resolve(spans[0].Style.Picture!.Source)).IsEqualTo("https://h.example/img/maps/map.png");
        await Assert.That(ImageSource.DisplayName(spans[0].Style.Picture!.Source)).IsEqualTo("map.png");
    }

    [Test]
    public async Task MxpImageOnOpenLineIsNotAPicture()
    {
        var spans = Mxp("before <IMAGE https://h.example/x.png> after");

        await Assert.That(spans.Any(s => s.Style.Picture is not null)).IsFalse();
    }

    [Test]
    public async Task MxpImageWithBareFileNameIsNotLoaded()
    {
        var spans = Mxp("\e[1z<IMAGE map.png>");

        await Assert.That(ImageSource.Resolve(spans[0].Style.Picture!.Source)).IsNull();
    }

    [Test]
    public async Task ImageInsideASendIsClickable()
    {
        var spans = Mxp("\e[1z<SEND href=\"look map\"><IMAGE https://h.example/m.png></SEND>");

        await Assert.That(spans[0].Style.Picture!.Source).IsEqualTo("https://h.example/m.png");
        await Assert.That(spans[0].Command()).IsEqualTo("look map");
    }

    [Test]
    public async Task ImagesAreTextWithoutMxpOrPueblo()
    {
        var spans = Plain("\e[1z<IMAGE https://h.example/x.png><img src=\"https://h.example/y.png\">");

        await Assert.That(spans.Any(s => s.Style.Picture is not null)).IsFalse();
    }

    [Test]
    public async Task CustomElementsExpand()
    {
        var decoder = new OutputDecoder();
        decoder.EnableMxp();
        decoder.Decode("\e[1z<!ELEMENT RName '<B><COLOR red>'>");

        var spans = Render(decoder, "\e[1z<RName>The Hall</RName>");

        await Assert.That(spans[0].Text).IsEqualTo("The Hall");
        await Assert.That(spans[0].Style.Has(TextAttributes.Bold)).IsTrue();
        await Assert.That(spans[0].Color()).IsEqualTo("#ff0000");
    }

    // ── Pueblo ────────────────────────────────────────────────────────────

    [Test]
    public async Task PuebloAnchorWithXchCmdIsACommandLink()
    {
        var spans = Pueblo("<a xch_cmd=\"go north\" xch_hint=\"Move\">North</a>");

        await Assert.That(spans.Count).IsEqualTo(1);
        await Assert.That(spans[0].Text).IsEqualTo("North");
        await Assert.That(spans[0].Command()).IsEqualTo("go north");
        await Assert.That(spans[0].Style.Interaction!.Hint).IsEqualTo("Move");
    }

    [Test]
    public async Task PuebloEntitiesAreDecoded()
    {
        await Assert.That(Text(Pueblo("5 &gt; 3 &amp;&amp; 2 &lt; 4 &quot;q&quot;"))).IsEqualTo("5 > 3 && 2 < 4 \"q\"");
        await Assert.That(Text(Pueblo("it&apos;s&nbsp;here"))).IsEqualTo("it's here");
        await Assert.That(Text(Pueblo("&#62;&#x3E;&#38;"))).IsEqualTo(">>&");
    }

    [Test]
    public async Task LoneAndUnknownAmpersandsStay()
    {
        await Assert.That(Text(Pueblo("Tom & Jerry"))).IsEqualTo("Tom & Jerry");
        await Assert.That(Text(Pueblo("x&notathing;y"))).IsEqualTo("x&notathing;y");
    }

    [Test]
    public async Task EntityInsideAnAttributeIsDecoded()
    {
        var spans = Pueblo("<a xch_cmd=\"say 5 &gt; 3\">go</a>");

        await Assert.That(spans[0].Command()).IsEqualTo("say 5 > 3");
    }

    [Test]
    public async Task GreaterThanInsideAQuotedAttributeDoesNotEndTheTag()
    {
        var spans = Pueblo("<a xch_cmd=\"say 5 > 3\">go</a>");

        await Assert.That(spans.Count).IsEqualTo(1);
        await Assert.That(spans[0].Text).IsEqualTo("go");
        await Assert.That(spans[0].Command()).IsEqualTo("say 5 > 3");
    }

    [Test]
    public async Task PuebloImgUsesSrcAndAlt()
    {
        var spans = Pueblo("Look: <img src=\"https://h.example/cat.gif\" alt=\"a cat\">!");

        await Assert.That(spans.Count).IsEqualTo(3);
        await Assert.That(spans[1].Style.Picture!.Source).IsEqualTo("https://h.example/cat.gif");
        await Assert.That(spans[1].Style.Picture!.Description).IsEqualTo("a cat");
        await Assert.That(spans[2].Text).IsEqualTo("!");
    }

    [Test]
    public async Task PuebloModeSwitchesAndPageClearShowNothing()
    {
        var spans = Pueblo("</xch_mudtext><img xch_mode=purehtml><xch_page clear=text>");

        await Assert.That(spans.All(s => s.Point is not null)).IsTrue();
    }

    [Test]
    [Arguments("javascript:alert(1)")]
    [Arguments("file:///etc/passwd")]
    [Arguments("/local/path.png")]
    [Arguments("content://media/1")]
    public async Task NonWebImageSourcesAreRefused(string src)
    {
        var spans = Pueblo($"<img src=\"{src}\">");

        await Assert.That(ImageSource.Resolve(spans[0].Style.Picture!.Source)).IsNull();
    }

    [Test]
    public async Task DataImageIsAccepted()
    {
        var spans = Pueblo("<img src=\"data:image/png;base64,iVBORw0KGgo=\">");

        await Assert.That(ImageSource.Resolve(spans[0].Style.Picture!.Source)).IsEqualTo("data:image/png;base64,iVBORw0KGgo=");
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

        var spans = Render(decoder, "<SEND href=\"x\">y</SEND>");

        await Assert.That(decoder.ActiveProtocol).IsNull();
        await Assert.That(Text(spans)).IsEqualTo("<SEND href=\"x\">y</SEND>");
        await Assert.That(spans[0].Color()).IsNull();
    }
}
