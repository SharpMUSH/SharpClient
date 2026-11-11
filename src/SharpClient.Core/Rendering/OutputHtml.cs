using MarkupString;
using MarkupString.Ansi;
using MarkupString.Html;

namespace SharpClient.Core.Rendering;

/// <summary>
/// How SharpClient's output view writes a decoded line: MarkupString's HTML, which the page shows with
/// <c>MarkupString.Web</c>'s stylesheet and script. Pictures go through <see cref="OutputImageEmitter"/>, and a
/// Highlight trigger through <see cref="TriggerHighlightEmitter"/>. Web links pass <see cref="OutputLinks"/>.
/// </summary>
public static class OutputHtml
{
    /// <summary>The registry that shows pictures.</summary>
    public static MarkupRegistry WithPictures { get; } = Build(showImages: true);

    /// <summary>The registry that shows a picture's name in its place.</summary>
    public static MarkupRegistry WithPictureNames { get; } = Build(showImages: false);

    /// <summary>The HTML for one line.</summary>
    public static string Render(MarkupText line, bool showImages = true)
    {
        ArgumentNullException.ThrowIfNull(line);
        return line.Render(MarkupFormat.Html, showImages ? WithPictures : WithPictureNames);
    }

    private static MarkupRegistry Build(bool showImages) =>
        MarkupRegistry.Empty
            .WithAnsi()
            .WithHtml()
            .With(new OutputImageEmitter(showImages))
            .With(TriggerHighlightEmitter.Instance)
            .WithLinks(OutputLinks.Instance);
}
