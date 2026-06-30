using Bunit;
using SharpClient.Core.Rendering;
using SharpClient.Core.Sessions;
using SharpClient.UI.Components;

namespace SharpClient.UI.Tests;

public sealed class OutputViewTests
{
    private static ScrollbackLine Line(params StyledSegment[] segments) => new(segments);

    [Test]
    public async Task RendersOneSpanPerSegmentWithRenderContractStyle()
    {
        using var ctx = new BunitContext();
        var line = Line(
            new StyledSegment("red", TextStyle.Default with { Foreground = AnsiColor.Indexed(1) }),
            new StyledSegment(" plain", TextStyle.Default));

        var cut = ctx.Render<OutputView>(p => p.Add(c => c.Lines, new[] { line }));

        var spans = cut.FindAll("span");
        await Assert.That(spans.Count).IsEqualTo(2);
        await Assert.That(spans[0].GetAttribute("style")).IsEqualTo("color:#e06c75;");
        await Assert.That(spans[0].TextContent).IsEqualTo("red");
        await Assert.That(spans[1].GetAttribute("style")).IsEqualTo("color:#c4d1c8;");
    }

    [Test]
    public async Task EmptySegmentRendersNonBreakingSpace()
    {
        using var ctx = new BunitContext();
        var line = Line(new StyledSegment(string.Empty, TextStyle.Default));

        var cut = ctx.Render<OutputView>(p => p.Add(c => c.Lines, new[] { line }));

        await Assert.That(cut.Find("span").TextContent).IsEqualTo(" ");
    }
}
