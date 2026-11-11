using Bunit;
using SharpClient.Core.Rendering;
using SharpClient.Core.Sessions;
using SharpClient.UI.Components;

namespace SharpClient.UI.Tests;

/// <summary>
/// The links, menus and pictures the output view writes. markupstring.js acts on the links and menus in a
/// browser; here, its calls back into the view are <see cref="OutputViewTests.CommandsFromTheScriptAreSent"/>.
/// </summary>
public sealed class OutputViewLinkTests
{
    private static ScrollbackLine[] Mxp(string raw)
    {
        var decoder = new OutputDecoder();
        decoder.EnableMxp();
        return [.. decoder.Decode(raw).Select(content => new ScrollbackLine(content))];
    }

    private static ScrollbackLine[] Pueblo(string raw)
    {
        var decoder = new OutputDecoder();
        decoder.EnablePueblo();
        decoder.Decode("</xch_mudtext><img xch_mode=html>");
        return [.. decoder.Decode(raw).Select(content => new ScrollbackLine(content))];
    }

    private static IRenderedComponent<OutputView> Render(BunitContext ctx, ScrollbackLine[] lines, bool showImages = true)
    {
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        return ctx.Render<OutputView>(p => p.Add(c => c.Lines, lines).Add(c => c.ShowImages, showImages));
    }

    [Test]
    public async Task CommandLinkIsAKeyboardReachableAnchorForTheScript()
    {
        using var ctx = new BunitContext();
        var cut = Render(ctx, Mxp("\e[1z<SEND href=\"go north\" hint=\"Move north\">North</SEND>"));

        var link = cut.Find("a.ms-cmd-link");
        await Assert.That(link.TextContent).IsEqualTo("North");
        await Assert.That(link.GetAttribute("xch_cmd")).IsEqualTo("go north");
        await Assert.That(link.GetAttribute("title")).IsEqualTo("Move north");
        await Assert.That(link.GetAttribute("role")).IsEqualTo("button");
        await Assert.That(link.GetAttribute("tabindex")).IsEqualTo("0");
    }

    [Test]
    public async Task CommandLinkKeepsItsColour()
    {
        using var ctx = new BunitContext();
        var cut = Render(ctx, Mxp("\e[1z<SEND href=\"hit\">\e[31mAttack\e[0m</SEND>"));

        // The stylesheet gives a link the accent colour only outside a span with a colour of its own.
        await Assert.That(cut.Find("a.ms-cmd-link").ParentElement!.GetAttribute("style")).StartsWith("color: var(--ms-ansi-1,");
    }

    [Test]
    public async Task PuebloCommandLinkIsACommandLink()
    {
        using var ctx = new BunitContext();
        var cut = Render(ctx, Pueblo("<a xch_cmd=\"go north\" xch_hint=\"Move\">North</a>"));

        await Assert.That(cut.Find("a.ms-cmd-link").GetAttribute("xch_cmd")).IsEqualTo("go north");
    }

    [Test]
    public async Task PlainTextHasNoLink()
    {
        using var ctx = new BunitContext();
        var cut = Render(ctx, Mxp("plain"));

        await Assert.That(cut.FindAll("a").Count).IsEqualTo(0);
        await Assert.That(cut.Find("div.sc-line").TextContent).IsEqualTo("plain");
    }

    [Test]
    public async Task SendWithSeveralCommandsIsAMenuForTheScript()
    {
        using var ctx = new BunitContext();
        var cut = Render(ctx, Mxp("\e[1z<SEND href=\"buy sword|look sword\" hint=\"Sword|Buy|Look\">sword</SEND>"));

        var menu = cut.Find("span.ms-menu");
        await Assert.That(menu.TextContent).IsEqualTo("sword");
        await Assert.That(menu.GetAttribute("aria-haspopup")).IsEqualTo("menu");
        await Assert.That(menu.GetAttribute("data-commands"))
            .IsEqualTo("[{\"label\":\"Buy\",\"command\":\"buy sword\"},{\"label\":\"Look\",\"command\":\"look sword\"}]");
    }

    [Test]
    public async Task WebLinkOpensInANewWindowWithoutAReferrer()
    {
        using var ctx = new BunitContext();
        var cut = Render(ctx, Mxp("see \e]8;;https://example.com/help\e\\help\e]8;;\e\\"));

        var link = cut.Find("a[href]");
        await Assert.That(link.GetAttribute("href")).IsEqualTo("https://example.com/help");
        await Assert.That(link.GetAttribute("target")).IsEqualTo("_blank");
        await Assert.That(link.GetAttribute("rel")).IsEqualTo("noopener noreferrer");
        await Assert.That(link.TextContent).IsEqualTo("help");
    }

    [Test]
    public async Task ImageRendersImgWithoutReferrer()
    {
        using var ctx = new BunitContext();
        var cut = Render(ctx, Pueblo("<img src=\"https://h.example/cat.gif\" alt=\"a cat\" width=40>"));

        var img = cut.Find("img.sc-image");
        await Assert.That(img.GetAttribute("src")).IsEqualTo("https://h.example/cat.gif");
        await Assert.That(img.GetAttribute("alt")).IsEqualTo("a cat");
        await Assert.That(img.GetAttribute("width")).IsEqualTo("40");
        await Assert.That(img.GetAttribute("referrerpolicy")).IsEqualTo("no-referrer");
        await Assert.That(cut.FindAll("a").Count).IsEqualTo(0);
    }

    [Test]
    public async Task MxpImageIsNamedAfterItsFile()
    {
        using var ctx = new BunitContext();
        var cut = Render(ctx, Mxp("\e[1z<IMAGE map.png URL=\"https://h.example/img/\" T=maps>"));

        var img = cut.Find("img.sc-image");
        await Assert.That(img.GetAttribute("src")).IsEqualTo("https://h.example/img/maps/map.png");
        await Assert.That(img.GetAttribute("alt")).IsEqualTo("map.png");
    }

    [Test]
    public async Task ImageInsideCommandLinkIsInsideTheLink()
    {
        using var ctx = new BunitContext();
        var cut = Render(ctx, Mxp("\e[1z<SEND href=\"look map\"><IMAGE https://h.example/m.png></SEND>"));

        await Assert.That(cut.Find("a.ms-cmd-link img.sc-image").GetAttribute("src")).IsEqualTo("https://h.example/m.png");
        await Assert.That(cut.Find("a.ms-cmd-link").GetAttribute("xch_cmd")).IsEqualTo("look map");
    }

    [Test]
    public async Task ImagesOffRendersTheTextInstead()
    {
        using var ctx = new BunitContext();
        var cut = Render(ctx, Pueblo("<img src=\"https://h.example/cat.gif\" alt=\"a cat\">"), showImages: false);

        await Assert.That(cut.FindAll("img").Count).IsEqualTo(0);
        await Assert.That(cut.Find("div.sc-line").TextContent).IsEqualTo("a cat");
    }

    [Test]
    public async Task TurningImagesOffRedrawsTheLines()
    {
        using var ctx = new BunitContext();
        var lines = Pueblo("<img src=\"https://h.example/cat.gif\" alt=\"a cat\">");
        var cut = Render(ctx, lines);

        cut.Render(p => p.Add(c => c.ShowImages, false));

        await Assert.That(cut.FindAll("img").Count).IsEqualTo(0);
        await Assert.That(cut.Find("div.sc-line").TextContent).IsEqualTo("a cat");
    }

    [Test]
    [Arguments("javascript:alert(1)")]
    [Arguments("file:///etc/passwd")]
    [Arguments("/local/path.png")]
    public async Task RefusedImageSourceShowsNothing(string src)
    {
        using var ctx = new BunitContext();
        var cut = Render(ctx, Pueblo($"<img src=\"{src}\" alt=\"x\">"));

        await Assert.That(cut.FindAll("img").Count).IsEqualTo(0);
        await Assert.That(cut.Find("div.sc-line").InnerHtml).IsEmpty();
    }
}
