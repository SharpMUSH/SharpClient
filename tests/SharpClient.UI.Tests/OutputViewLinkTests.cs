using Bunit;
using SharpClient.Core.Rendering;
using SharpClient.Core.Sessions;
using SharpClient.UI.Components;

namespace SharpClient.UI.Tests;

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

    [Test]
    public async Task CommandLinkRendersClickableButtonThatSendsCommand()
    {
        using var ctx = new BunitContext();
        string? sent = null;
        var cut = ctx.Render<OutputView>(p => p
            .Add(c => c.Lines, Mxp("\e[1z<SEND href=\"go north\" hint=\"Move north\">North</SEND>"))
            .Add(c => c.OnSendCommand, cmd => { sent = cmd; return Task.CompletedTask; }));

        var button = cut.Find("button.sc-link");
        await Assert.That(button.TextContent).IsEqualTo("North");
        await Assert.That(button.GetAttribute("title")).IsEqualTo("Move north");
        button.Click();
        await Assert.That(sent).IsEqualTo("go north");
    }

    [Test]
    public async Task CommandLinkKeepsItsColour()
    {
        using var ctx = new BunitContext();
        var cut = ctx.Render<OutputView>(p => p
            .Add(c => c.Lines, Mxp("\e[1z<SEND href=\"hit\">\e[31mAttack\e[0m</SEND>"))
            .Add(c => c.OnSendCommand, _ => Task.CompletedTask));

        await Assert.That(cut.Find("button.sc-link").GetAttribute("style")).IsEqualTo("color:#e06c75");
    }

    [Test]
    public async Task CommandLinkWithoutAHandlerIsText()
    {
        using var ctx = new BunitContext();
        var cut = ctx.Render<OutputView>(p => p.Add(c => c.Lines, Mxp("\e[1z<SEND href=\"go\">go</SEND>")));

        await Assert.That(cut.FindAll("button").Count).IsEqualTo(0);
        await Assert.That(cut.Find("span").TextContent).IsEqualTo("go");
    }

    [Test]
    public async Task PuebloCommandLinkIsAButton()
    {
        using var ctx = new BunitContext();
        string? sent = null;
        var cut = ctx.Render<OutputView>(p => p
            .Add(c => c.Lines, Pueblo("<a xch_cmd=\"go north\" xch_hint=\"Move\">North</a>"))
            .Add(c => c.OnSendCommand, cmd => { sent = cmd; return Task.CompletedTask; }));

        cut.Find("button.sc-link").Click();
        await Assert.That(sent).IsEqualTo("go north");
    }

    [Test]
    public async Task PlainTextStillRendersSpanNotButton()
    {
        using var ctx = new BunitContext();
        var cut = ctx.Render<OutputView>(p => p
            .Add(c => c.Lines, Mxp("plain"))
            .Add(c => c.OnSendCommand, _ => Task.CompletedTask));

        await Assert.That(cut.FindAll("button.sc-link").Count).IsEqualTo(0);
        await Assert.That(cut.Find("span").TextContent).IsEqualTo("plain");
    }

    [Test]
    public async Task MenuSendsTheFirstCommandAndListsTheRest()
    {
        using var ctx = new BunitContext();
        var sent = new List<string>();
        var cut = ctx.Render<OutputView>(p => p
            .Add(c => c.Lines, Mxp("\e[1z<SEND href=\"buy sword|look sword\" hint=\"Sword|Buy|Look\">sword</SEND>"))
            .Add(c => c.OnSendCommand, cmd => { sent.Add(cmd); return Task.CompletedTask; }));

        cut.Find("button.sc-link").Click();
        await Assert.That(cut.FindAll("button.sc-menu-choice").Count).IsEqualTo(0);

        cut.Find("button.sc-menu-toggle").Click();
        var choices = cut.FindAll("button.sc-menu-choice");
        await Assert.That(choices.Select(c => c.TextContent)).IsEquivalentTo(["Buy", "Look"]);
        choices[1].Click();

        await Assert.That(sent).IsEquivalentTo(["buy sword", "look sword"]);
        await Assert.That(cut.FindAll("button.sc-menu-choice").Count).IsEqualTo(0);
    }

    [Test]
    public async Task WebLinkOpensInANewWindowWithoutAReferrer()
    {
        using var ctx = new BunitContext();
        var cut = ctx.Render<OutputView>(p => p.Add(c => c.Lines,
            Mxp("see \e]8;;https://example.com/help\e\\help\e]8;;\e\\")));

        var link = cut.Find("a.sc-link");
        await Assert.That(link.GetAttribute("href")).IsEqualTo("https://example.com/help");
        await Assert.That(link.GetAttribute("target")).IsEqualTo("_blank");
        await Assert.That(link.GetAttribute("rel")).IsEqualTo("noopener noreferrer");
        await Assert.That(link.TextContent).IsEqualTo("help");
    }

    [Test]
    public async Task ImageRendersImgWithoutReferrer()
    {
        using var ctx = new BunitContext();
        var cut = ctx.Render<OutputView>(p => p.Add(c => c.Lines,
            Pueblo("<img src=\"https://h.example/cat.gif\" alt=\"a cat\" width=40>")));

        var img = cut.Find("img.sc-image");
        await Assert.That(img.GetAttribute("src")).IsEqualTo("https://h.example/cat.gif");
        await Assert.That(img.GetAttribute("alt")).IsEqualTo("a cat");
        await Assert.That(img.GetAttribute("width")).IsEqualTo("40");
        await Assert.That(img.GetAttribute("referrerpolicy")).IsEqualTo("no-referrer");
        await Assert.That(cut.FindAll("button").Count).IsEqualTo(0);
    }

    [Test]
    public async Task MxpImageIsNamedAfterItsFile()
    {
        using var ctx = new BunitContext();
        var cut = ctx.Render<OutputView>(p => p.Add(c => c.Lines,
            Mxp("\e[1z<IMAGE map.png URL=\"https://h.example/img/\" T=maps>")));

        var img = cut.Find("img.sc-image");
        await Assert.That(img.GetAttribute("src")).IsEqualTo("https://h.example/img/maps/map.png");
        await Assert.That(img.GetAttribute("alt")).IsEqualTo("map.png");
    }

    [Test]
    public async Task ImageInsideCommandLinkIsAClickableButton()
    {
        using var ctx = new BunitContext();
        string? sent = null;
        var cut = ctx.Render<OutputView>(p => p
            .Add(c => c.Lines, Mxp("\e[1z<SEND href=\"look map\"><IMAGE https://h.example/m.png></SEND>"))
            .Add(c => c.OnSendCommand, cmd => { sent = cmd; return Task.CompletedTask; }));

        cut.Find("button.sc-image-link img.sc-image");
        cut.Find("button.sc-image-link").Click();
        await Assert.That(sent).IsEqualTo("look map");
    }

    [Test]
    public async Task ImagesOffRendersTheTextInstead()
    {
        using var ctx = new BunitContext();
        var cut = ctx.Render<OutputView>(p => p
            .Add(c => c.Lines, Pueblo("<img src=\"https://h.example/cat.gif\" alt=\"a cat\">"))
            .Add(c => c.ShowImages, false));

        await Assert.That(cut.FindAll("img").Count).IsEqualTo(0);
        await Assert.That(cut.Markup).Contains("a cat");
    }

    [Test]
    [Arguments("javascript:alert(1)")]
    [Arguments("file:///etc/passwd")]
    [Arguments("/local/path.png")]
    public async Task RefusedImageSourceShowsNothing(string src)
    {
        using var ctx = new BunitContext();
        var cut = ctx.Render<OutputView>(p => p.Add(c => c.Lines, Pueblo($"<img src=\"{src}\" alt=\"x\">")));

        await Assert.That(cut.FindAll("img").Count).IsEqualTo(0);
        await Assert.That(cut.FindAll("span").Count).IsEqualTo(0);
    }
}
