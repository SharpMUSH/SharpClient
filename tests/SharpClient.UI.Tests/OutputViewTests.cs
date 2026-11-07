using Bunit;
using SharpClient.Core.Rendering;
using SharpClient.Core.Sessions;
using SharpClient.UI.Components;

namespace SharpClient.UI.Tests;

public sealed class OutputViewTests
{
    private static ScrollbackLine[] Lines(string raw) =>
        [.. new OutputDecoder().Decode(raw).Select(content => new ScrollbackLine(content))];

    [Test]
    public async Task RendersOneSpanPerStyleWithTheClientPalette()
    {
        using var ctx = new BunitContext();

        var cut = ctx.Render<OutputView>(p => p.Add(c => c.Lines, Lines("\e[31mred\e[0m plain")));

        var spans = cut.FindAll("span");
        await Assert.That(spans.Count).IsEqualTo(2);
        await Assert.That(spans[0].GetAttribute("style")).IsEqualTo("color:#e06c75");
        await Assert.That(spans[0].TextContent).IsEqualTo("red");
        // Default text takes .sc-line's colour.
        await Assert.That(spans[1].GetAttribute("style")).IsNull();
        await Assert.That(spans[1].TextContent).IsEqualTo(" plain");
    }

    [Test]
    public async Task BackgroundGetsPadding()
    {
        using var ctx = new BunitContext();

        var cut = ctx.Render<OutputView>(p => p.Add(c => c.Lines, Lines("\e[44mbg")));

        await Assert.That(cut.Find("span").GetAttribute("style")).IsEqualTo("background-color:#61afef;padding:0 2px");
    }

    [Test]
    public async Task AttributesAreDrawn()
    {
        using var ctx = new BunitContext();

        var cut = ctx.Render<OutputView>(p => p.Add(c => c.Lines, Lines("\e[1;3;4;9mx\e[0m \e[5mblink")));

        var spans = cut.FindAll("span");
        await Assert.That(spans[0].GetAttribute("style")).IsEqualTo("font-weight:bold;font-style:italic;text-decoration:underline line-through");
        await Assert.That(spans[2].ClassList).Contains("ms-blink");
    }

    [Test]
    public async Task EmptyLineRendersAnEmptyLine()
    {
        using var ctx = new BunitContext();

        var cut = ctx.Render<OutputView>(p => p.Add(c => c.Lines, Lines(string.Empty)));

        await Assert.That(cut.FindAll("div.sc-line").Count).IsEqualTo(1);
        await Assert.That(cut.FindAll("span").Count).IsEqualTo(0);
    }

    [Test]
    public async Task TriggerHighlightReplacesTheServersColour()
    {
        using var ctx = new BunitContext();
        var rule = new SharpClient.Core.Domain.TriggerRule
        {
            Kind = SharpClient.Core.Domain.TriggerKind.Substring,
            Pattern = "danger",
            Action = SharpClient.Core.Domain.TriggerActionKind.Highlight,
            ActionValue = "9",
        };
        const string raw = "\e[32mdanger\e[0m ahead";
        var outcome = new SharpClient.Core.Triggers.TriggerEngine().Apply(raw, new OutputDecoder().Decode(raw), [rule]);

        var cut = ctx.Render<OutputView>(p => p.Add(c => c.Lines, outcome.Lines.Select(l => new ScrollbackLine(l)).ToArray()));

        await Assert.That(cut.Find("span").GetAttribute("style")).IsEqualTo("color:#ff8088");
        await Assert.That(cut.Find("span").TextContent).IsEqualTo("danger ahead");
    }
}
