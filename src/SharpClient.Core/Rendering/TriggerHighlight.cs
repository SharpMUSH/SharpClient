using System.Buffers;
using MarkupString;

namespace SharpClient.Core.Rendering;

/// <summary>A Highlight trigger's colour over a line: palette entry <paramref name="Index"/> (0–255).</summary>
public sealed record TriggerHighlightMarkup(int Index) : IMarkup;

/// <summary>
/// Writes <see cref="TriggerHighlightMarkup"/> as a <c>span.sc-highlight</c> that sets <c>--sc-highlight</c>.
/// The stylesheet gives that colour to everything inside, over the server's own colours.
/// </summary>
public sealed class TriggerHighlightEmitter : IMarkupEmitter
{
    public static TriggerHighlightEmitter Instance { get; } = new();

    private TriggerHighlightEmitter()
    {
    }

    public Type MarkupType => typeof(TriggerHighlightMarkup);

    public MarkupFormat Format => MarkupFormat.Html;

    public void Emit(IMarkup markup, ReadOnlySpan<char> body, in EmitContext context, IBufferWriter<char> output)
    {
        ArgumentNullException.ThrowIfNull(markup);
        ArgumentNullException.ThrowIfNull(output);

        var highlight = (TriggerHighlightMarkup)markup;
        if (context.StartsRegion(markup))
        {
            output.Write("<span class=\"sc-highlight\" style=\"--sc-highlight: ");
            output.Write(OutputPalette.Hex(OutputPalette.Instance.Entry(Math.Clamp(highlight.Index, 0, 255))));
            output.Write("\">");
        }

        output.Write(body);

        if (context.EndsRegion(markup))
        {
            output.Write("</span>");
        }
    }
}
