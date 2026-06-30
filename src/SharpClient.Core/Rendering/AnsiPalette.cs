namespace SharpClient.Core.Rendering;

public static class AnsiPalette
{
    // Shared across the Rendering layer — SegmentStyle references this rather than duplicating.
    internal const string PhosphorDefault = "#c4d1c8";

    private static readonly string[] Base16 =
    [
        "#3a3f4b", "#e06c75", "#8fc16f", "#e5c07b", "#61afef", "#c678dd", "#56b6c2", "#abb2bf",
        "#5c6672", "#ff8088", "#b5e890", "#ffd596", "#7cc4ff", "#e29bf0", "#6fd3df", "#e8edf2",
    ];

    private static readonly int[] CubeSteps = [0, 95, 135, 175, 215, 255];

    public static string ToHex(int index)
    {
        if (index is >= 0 and < 16)
        {
            return Base16[index];
        }

        if (index is >= 16 and <= 231)
        {
            var n = index - 16;
            var r = CubeSteps[n / 36 % 6];
            var g = CubeSteps[n / 6 % 6];
            var b = CubeSteps[n % 6];
            return Rgb(r, g, b);
        }

        if (index is >= 232 and <= 255)
        {
            var v = 8 + (index - 232) * 10;
            return Rgb(v, v, v);
        }

        return PhosphorDefault;
    }

    private static string Rgb(int r, int g, int b) =>
        $"#{r:x2}{g:x2}{b:x2}";
}
