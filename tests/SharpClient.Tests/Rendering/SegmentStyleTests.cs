using SharpClient.Core.Rendering;

namespace SharpClient.Tests.Rendering;

public sealed class SegmentStyleTests
{
    private static StyledSegment Seg(TextStyle style) => new("x", style);

    [Test]
    public async Task DefaultIsPhosphorForeground()
    {
        var css = SegmentStyle.ToCss(Seg(TextStyle.Default));

        await Assert.That(css).IsEqualTo("color:#c4d1c8;");
    }

    [Test]
    public async Task IndexedForegroundUsesPalette()
    {
        var css = SegmentStyle.ToCss(Seg(TextStyle.Default with { Foreground = AnsiColor.Indexed(1) }));

        await Assert.That(css).IsEqualTo("color:#e06c75;");
    }

    [Test]
    public async Task IndexedBackgroundAddsPadding()
    {
        var css = SegmentStyle.ToCss(Seg(TextStyle.Default with { Background = AnsiColor.Indexed(4) }));

        await Assert.That(css).IsEqualTo("color:#c4d1c8;background:#61afef;padding:0 2px;");
    }

    [Test]
    public async Task InverseSwapsForegroundAndBackground()
    {
        var css = SegmentStyle.ToCss(Seg(TextStyle.Default with { Foreground = AnsiColor.Indexed(2), Inverse = true }));

        await Assert.That(css).IsEqualTo("color:#090c10;background:#8fc16f;padding:0 2px;");
    }

    [Test]
    public async Task InverseWithDefaultForegroundUsesPhosphorBackground()
    {
        var css = SegmentStyle.ToCss(Seg(TextStyle.Default with { Inverse = true }));

        await Assert.That(css).IsEqualTo("color:#090c10;background:#c4d1c8;padding:0 2px;");
    }

    [Test]
    public async Task BoldAndUnderlineAppend()
    {
        var css = SegmentStyle.ToCss(Seg(TextStyle.Default with { Bold = true, Underline = true }));

        await Assert.That(css).IsEqualTo("color:#c4d1c8;font-weight:700;text-decoration:underline;");
    }
}
