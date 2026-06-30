using System.Text;

namespace SharpClient.Core.Rendering;

public static class SegmentStyle
{
    // PhosphorDefault is the single source of truth: AnsiPalette.PhosphorDefault (internal const).
    private const string OutputBackground = "#090c10";

    public static string ToCss(StyledSegment segment)
    {
        var style = segment.Style;
        var fg = Resolve(style.Foreground, AnsiPalette.PhosphorDefault);
        string? bg = style.Background.Kind == AnsiColorKind.Indexed
            ? AnsiPalette.ToHex(style.Background.Index)
            : null;

        if (style.Inverse)
        {
            (fg, bg) = (OutputBackground, fg);
        }

        var sb = new StringBuilder();
        sb.Append("color:").Append(fg).Append(';');
        if (bg is not null)
        {
            sb.Append("background:").Append(bg).Append(';');
            sb.Append("padding:0 2px;");
        }

        if (style.Bold)
        {
            sb.Append("font-weight:700;");
        }

        if (style.Underline)
        {
            sb.Append("text-decoration:underline;");
        }

        return sb.ToString();
    }

    private static string Resolve(AnsiColor color, string fallback) =>
        color.Kind == AnsiColorKind.Indexed
            ? AnsiPalette.ToHex(color.Index)
            : fallback;
}
