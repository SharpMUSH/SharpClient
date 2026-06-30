using Bunit;
using SharpClient.Core.Rendering;
using SharpClient.Core.Sessions;
using SharpClient.UI.Components;

namespace SharpClient.UI.Tests;

public sealed class OutputViewLinkTests
{
    private static ScrollbackLine Line(params StyledSegment[] segments) => new(segments);

    [Test]
    public async Task CommandSegmentRendersClickableButtonThatSendsCommand()
    {
        using var ctx = new BunitContext();
        string? sent = null;
        var line = Line(new StyledSegment("North", TextStyle.Default) { Command = "go north", Hint = "Move north" });

        var cut = ctx.Render<OutputView>(p => p
            .Add(c => c.Lines, new[] { line })
            .Add(c => c.OnSendCommand, cmd => { sent = cmd; return Task.CompletedTask; }));

        var button = cut.Find("button.sc-link");
        await Assert.That(button.TextContent).IsEqualTo("North");
        await Assert.That(button.GetAttribute("title")).IsEqualTo("Move north");

        button.Click();
        await Assert.That(sent).IsEqualTo("go north");
    }

    [Test]
    public async Task PlainSegmentStillRendersSpanNotButton()
    {
        using var ctx = new BunitContext();
        var line = Line(new StyledSegment("plain", TextStyle.Default));

        var cut = ctx.Render<OutputView>(p => p
            .Add(c => c.Lines, new[] { line })
            .Add(c => c.OnSendCommand, _ => Task.CompletedTask));

        await Assert.That(cut.FindAll("button.sc-link").Count).IsEqualTo(0);
        await Assert.That(cut.Find("span").TextContent).IsEqualTo("plain");
    }
}
