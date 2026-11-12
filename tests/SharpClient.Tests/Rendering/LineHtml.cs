using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using MarkupString;
using SharpClient.Core.Rendering;
using SharpClient.Core.Sessions;

namespace SharpClient.Tests.Rendering;

/// <summary>What the output view shows for a line (<see cref="OutputHtml"/>), parsed, for asserting on.</summary>
internal static partial class LineHtml
{
    private static readonly HtmlParser s_parser = new();

    // What the stylesheet gives MarkupString's colour properties: the client's palette (OutputView sets
    // --ms-ansi-N from OutputPalette), and the output's text colour and background (--pho, --outbg).
    private const string OutputForeground = "#c4d1c8";
    private const string OutputBackground = "#090c10";

    public static string Html(this ScrollbackLine line, bool showImages = true) => OutputHtml.Render(line.Content, showImages);

    /// <summary>The line's elements, inside a <c>div</c> standing for <c>.sc-line</c>.</summary>
    public static IElement Dom(this MarkupText line, bool showImages = true) =>
        s_parser.ParseDocument("<div>" + OutputHtml.Render(line, showImages) + "</div>").Body!.FirstElementChild!;

    public static IElement Dom(this ScrollbackLine line, bool showImages = true) => line.Content.Dom(showImages);

    /// <summary>The commands the line's command links send, in order.</summary>
    public static IReadOnlyList<string> Commands(this IElement root) =>
        [.. root.QuerySelectorAll("a[xch_cmd]").Select(a => a.GetAttribute("xch_cmd")!)];

    /// <summary>
    /// The colour the element's text is drawn in, as <c>#rrggbb</c>, with the stylesheet's custom properties
    /// resolved; null for the output's default.
    /// </summary>
    public static string? Color(this IElement element) => Resolve(Declaration(element, "color"));

    public static string? BackgroundColor(this IElement element) => Resolve(Declaration(element, "background-color"));

    /// <summary>Palette entry <paramref name="index"/> in the client's palette.</summary>
    public static string Hex(int index) => OutputPalette.Hex(OutputPalette.Instance.Entry(index));

    /// <summary>The nearest value of <paramref name="property"/> in a style attribute, from the element outwards.</summary>
    private static string? Declaration(IElement element, string property)
    {
        for (var at = element; at is not null; at = at.ParentElement)
        {
            foreach (var declaration in (at.GetAttribute("style") ?? string.Empty).Split(';'))
            {
                var colon = declaration.IndexOf(':', StringComparison.Ordinal);
                if (colon > 0 && declaration[..colon].Trim() == property)
                {
                    return declaration[(colon + 1)..].Trim();
                }
            }
        }

        return null;
    }

    private static string? Resolve(string? value)
    {
        if (value is null)
        {
            return null;
        }

        if (AnsiSlot().Match(value) is { Success: true } slot)
        {
            return Hex(int.Parse(slot.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture));
        }

        if (value.StartsWith("var(--ms-fg", StringComparison.Ordinal))
        {
            return OutputForeground;
        }

        return value.StartsWith("var(--ms-bg", StringComparison.Ordinal) ? OutputBackground : value;
    }

    // var(--ms-ansi-N, …) or var(--ms-ansi-bg-N, var(--ms-ansi-N, …)): OutputView sets only --ms-ansi-N.
    [GeneratedRegex(@"^var\(--ms-ansi-(?:bg-)?(\d+),")]
    private static partial Regex AnsiSlot();
}
