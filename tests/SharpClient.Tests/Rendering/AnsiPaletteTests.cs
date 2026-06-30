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

    // ── New tests ────────────────────────────────────────────────────────────

    [Test]
    public async Task GrayscaleRampEndsAtIndex255()
    {
        // v = 8 + (255 − 232) × 10 = 8 + 230 = 238 = 0xEE
        await Assert.That(AnsiPalette.ToHex(255)).IsEqualTo("#eeeeee");
    }

    [Test]
    public async Task MidCubeIndex100Maps()
    {
        // n = 100 − 16 = 84; r = CubeSteps[84/36%6]=CubeSteps[2]=135=0x87
        // g = CubeSteps[84/6%6]=CubeSteps[2]=135=0x87; b = CubeSteps[84%6]=CubeSteps[0]=0=0x00
        await Assert.That(AnsiPalette.ToHex(100)).IsEqualTo("#878700");
    }

    [Test]
    public async Task NegativeIndexFallsBackToPhosphor()
    {
        await Assert.That(AnsiPalette.ToHex(-1)).IsEqualTo("#c4d1c8");
    }
}
