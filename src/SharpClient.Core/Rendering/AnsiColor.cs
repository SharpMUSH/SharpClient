namespace SharpClient.Core.Rendering;

public enum AnsiColorKind
{
    Default,
    Indexed,

    /// <summary>24-bit colour (SGR 38;2 / 48;2). <see cref="AnsiColor.Index"/> holds 0xRRGGBB.</summary>
    Rgb,
}

public readonly record struct AnsiColor(AnsiColorKind Kind, int Index)
{
    public static AnsiColor Default => new(AnsiColorKind.Default, 0);

    public static AnsiColor Indexed(int index) => new(AnsiColorKind.Indexed, index);

    public static AnsiColor Rgb(int r, int g, int b) =>
        new(AnsiColorKind.Rgb, (Math.Clamp(r, 0, 255) << 16) | (Math.Clamp(g, 0, 255) << 8) | Math.Clamp(b, 0, 255));
}
