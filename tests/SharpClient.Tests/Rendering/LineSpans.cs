using MarkupString.Display;
using SharpClient.Core.Rendering;
using SharpClient.Core.Sessions;

namespace SharpClient.Tests.Rendering;

/// <summary>What the output view draws for a line, for asserting on.</summary>
internal static class LineSpans
{
    public static IReadOnlyList<DisplaySpan> Spans(this ScrollbackLine line) => OutputDisplay.Render(line.Content);

    public static IReadOnlyList<DisplaySpan> Spans(this MarkupString.MarkupText line) => OutputDisplay.Render(line);

    /// <summary>The command a span sends when clicked, or null.</summary>
    public static string? Command(this DisplaySpan span) =>
        span.Style.Interaction is { Kind: InteractionKind.Command } interaction ? interaction.Target : null;

    /// <summary>The CSS colour of the span's text, or null for the output's default.</summary>
    public static string? Color(this DisplaySpan span) =>
        span.Style.Foreground is { } color ? SpanRenderer.Hex(color) : null;

    public static string? BackgroundColor(this DisplaySpan span) =>
        span.Style.Background is { } color ? SpanRenderer.Hex(color) : null;
}
