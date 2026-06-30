using SharpClient.Core.Rendering;

namespace SharpClient.Tests.Rendering;

public sealed class AnsiParserTests
{
    [Test]
    public async Task DefaultStyleHasDefaultColours()
    {
        var style = TextStyle.Default;

        await Assert.That(style.Foreground).IsEqualTo(AnsiColor.Default);
        await Assert.That(style.Background).IsEqualTo(AnsiColor.Default);
        await Assert.That(style.Bold).IsFalse();
    }

    [Test]
    public async Task IndexedColourCarriesItsIndex()
    {
        var colour = AnsiColor.Indexed(200);

        await Assert.That(colour.Kind).IsEqualTo(AnsiColorKind.Indexed);
        await Assert.That(colour.Index).IsEqualTo(200);
    }

    [Test]
    public async Task PlainTextIsOneDefaultSegment()
    {
        var segments = AnsiParser.Parse("hello world");

        await Assert.That(segments.Count).IsEqualTo(1);
        await Assert.That(segments[0].Text).IsEqualTo("hello world");
        await Assert.That(segments[0].Style).IsEqualTo(TextStyle.Default);
    }

    [Test]
    public async Task RedForegroundAppliesToFollowingText()
    {
        var segments = AnsiParser.Parse("\e[31mred\e[0m normal");

        await Assert.That(segments.Count).IsEqualTo(2);
        await Assert.That(segments[0].Text).IsEqualTo("red");
        await Assert.That(segments[0].Style.Foreground).IsEqualTo(AnsiColor.Indexed(1));
        await Assert.That(segments[1].Text).IsEqualTo(" normal");
        await Assert.That(segments[1].Style.Foreground).IsEqualTo(AnsiColor.Default);
    }

    [Test]
    public async Task BrightForegroundMapsToHighIndex()
    {
        var segments = AnsiParser.Parse("\e[92mbright");

        await Assert.That(segments[0].Style.Foreground).IsEqualTo(AnsiColor.Indexed(10));
    }

    [Test]
    public async Task Xterm256ForegroundIsParsed()
    {
        var segments = AnsiParser.Parse("\e[38;5;208morange");

        await Assert.That(segments[0].Style.Foreground).IsEqualTo(AnsiColor.Indexed(208));
    }

    [Test]
    public async Task BoldAndUnderlineCombine()
    {
        var segments = AnsiParser.Parse("\e[1;4mhi");

        await Assert.That(segments[0].Style.Bold).IsTrue();
        await Assert.That(segments[0].Style.Underline).IsTrue();
    }

    [Test]
    public async Task NonSgrCsiIsStripped()
    {
        var segments = AnsiParser.Parse("a\e[2Kb");

        await Assert.That(segments.Count).IsEqualTo(1);
        await Assert.That(segments[0].Text).IsEqualTo("ab");
    }

    [Test]
    public async Task EmptyLineProducesNoSegments()
    {
        var segments = AnsiParser.Parse(string.Empty);

        await Assert.That(segments.Count).IsEqualTo(0);
    }

    [Test]
    public async Task TruecolorForegroundDoesNotCorruptBackground()
    {
        var segments = AnsiParser.Parse("\e[38;2;40;40;40mX");

        await Assert.That(segments[0].Style.Background).IsEqualTo(AnsiColor.Default);
        await Assert.That(segments[0].Style.Foreground).IsEqualTo(AnsiColor.Default);
    }

    [Test]
    public async Task TruecolorBackgroundIsConsumed()
    {
        var segments = AnsiParser.Parse("\e[48;2;10;20;30mX");

        await Assert.That(segments[0].Style.Foreground).IsEqualTo(AnsiColor.Default);
        await Assert.That(segments[0].Style.Background).IsEqualTo(AnsiColor.Default);
        await Assert.That(segments[0].Text).IsEqualTo("X");
    }

    [Test]
    public async Task TrailingNewlineIsPreservedInSegmentText()
    {
        // A line delivered from the server may include a trailing '\n'.
        // AnsiParser is not responsible for stripping it — the raw character
        // must survive into the segment text unchanged.
        var segments = AnsiParser.Parse("hello\n");

        await Assert.That(segments.Count).IsEqualTo(1);
        await Assert.That(segments[0].Text).IsEqualTo("hello\n");
    }
}
