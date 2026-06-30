using SharpClient.Core.Rendering;

namespace SharpClient.Tests.Rendering;

public sealed class AnsiPaletteTests
{
    [Test]
    [Arguments(0, "#3a3f4b")]
    [Arguments(1, "#e06c75")]
    [Arguments(7, "#abb2bf")]
    [Arguments(8, "#5c6672")]
    [Arguments(15, "#e8edf2")]
    public async Task BaseSixteenMatchDesignTokens(int index, string hex)
    {
        await Assert.That(AnsiPalette.ToHex(index)).IsEqualTo(hex);
    }

    [Test]
    public async Task CubeBlackIsIndex16()
    {
        await Assert.That(AnsiPalette.ToHex(16)).IsEqualTo("#000000");
    }

    [Test]
    public async Task CubeWhiteIsIndex231()
    {
        await Assert.That(AnsiPalette.ToHex(231)).IsEqualTo("#ffffff");
    }

    [Test]
    public async Task GrayscaleRampStartsDark()
    {
        await Assert.That(AnsiPalette.ToHex(232)).IsEqualTo("#080808");
    }

    [Test]
    public async Task OutOfRangeFallsBackToPhosphor()
    {
        await Assert.That(AnsiPalette.ToHex(999)).IsEqualTo("#c4d1c8");
    }
}
