using MarkupString;
using MarkupString.Display;

namespace SharpClient.Core.Rendering;

/// <summary>A Highlight trigger's colour over a line: palette entry <paramref name="Index"/> (0–255).</summary>
public sealed record TriggerHighlightMarkup(int Index) : IMarkup;

/// <summary>
/// Draws <see cref="TriggerHighlightMarkup"/> as the text colour. Added after the ANSI styles, so it
/// replaces whatever colour the server gave, and inverse text still swaps it into the background.
/// </summary>
public sealed class TriggerHighlightStyles : IStyleContributor
{
    public static TriggerHighlightStyles Instance { get; } = new();

    private TriggerHighlightStyles()
    {
    }

    public void Contribute(MarkupSet markups, StyleBuilder style, DisplayOptions options)
    {
        ArgumentNullException.ThrowIfNull(markups);
        ArgumentNullException.ThrowIfNull(style);
        ArgumentNullException.ThrowIfNull(options);

        // Innermost first: the first highlight found is the one applied last.
        foreach (var markup in markups)
        {
            if (markup is TriggerHighlightMarkup highlight)
            {
                var palette = options.Colors as OutputPalette ?? OutputPalette.Instance;
                style.Foreground = palette.Entry(Math.Clamp(highlight.Index, 0, 255));
                return;
            }
        }
    }
}
