using MarkupString;
using MarkupString.Ansi.Decoding;

namespace SharpClient.Core.Rendering;

/// <summary>
/// SharpClient's colours for the sixteen standard ANSI colours, and the output's own foreground and
/// background. The 256-colour cube and grey ramp are xterm's, as before.
/// </summary>
public sealed class OutputPalette : AnsiColors
{
    public static OutputPalette Instance { get; } = new();

    /// <summary>Text with no colour of its own (the CSS variable <c>--pho</c>).</summary>
    public static RgbColor Phosphor { get; } = new(0xc4, 0xd1, 0xc8);

    /// <summary>The output area's background (the CSS variable <c>--outbg</c>).</summary>
    public static RgbColor Background { get; } = new(0x09, 0x0c, 0x10);

    private static readonly RgbColor[] s_base16 =
    [
        Hex(0x3a3f4b), Hex(0xe06c75), Hex(0x8fc16f), Hex(0xe5c07b), Hex(0x61afef), Hex(0xc678dd), Hex(0x56b6c2), Hex(0xabb2bf),
        Hex(0x5c6672), Hex(0xff8088), Hex(0xb5e890), Hex(0xffd596), Hex(0x7cc4ff), Hex(0xe29bf0), Hex(0x6fd3df), Hex(0xe8edf2),
    ];

    private OutputPalette()
    {
    }

    public override RgbColor Standard(int index, bool bright) => s_base16[(index & 7) + (bright ? 8 : 0)];

    public override RgbColor Xterm(int index) => index is >= 0 and < 16 ? s_base16[index] : base.Xterm(index);

    /// <summary>Palette entry <paramref name="index"/> (0–255) in this palette.</summary>
    public RgbColor Entry(int index) => index < 16 ? Standard(index & 7, index >= 8) : Xterm(index);

    private static RgbColor Hex(int rgb) => new((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
}
