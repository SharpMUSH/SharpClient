using System.Buffers;
using System.Globalization;
using System.Net;
using MarkupString;

namespace SharpClient.Core.Rendering;

/// <summary>
/// Writes an MXP <c>&lt;IMAGE&gt;</c> or Pueblo <c>&lt;img&gt;</c> for the output view. Only an address
/// <see cref="ImageSource.Resolve"/> accepts is loaded; any other shows nothing. With images off, the
/// picture's description or file name is shown instead.
/// </summary>
public sealed class OutputImageEmitter(bool showImages) : IMarkupEmitter
{
    public Type MarkupType => typeof(ImageMarkup);

    public MarkupFormat Format => MarkupFormat.Html;

    public void Emit(IMarkup markup, ReadOnlySpan<char> body, in EmitContext context, IBufferWriter<char> output)
    {
        ArgumentNullException.ThrowIfNull(markup);
        ArgumentNullException.ThrowIfNull(output);

        var image = (ImageMarkup)markup;

        // A picture sized in cells covers several runs; it is drawn once, where it starts.
        if (!image.StartsPicture(context) || ImageSource.Resolve(image.Source) is not { } source)
        {
            return;
        }

        var name = WebUtility.HtmlEncode(image.Description ?? ImageSource.DisplayName(image.Source));
        if (!showImages)
        {
            output.Write(name);
            return;
        }

        // no-referrer: the image host learns nothing about where it was shown.
        output.Write("<img class=\"ms-image sc-image\" src=\"");
        output.Write(WebUtility.HtmlEncode(source));
        output.Write("\" alt=\"");
        output.Write(name);
        output.Write("\" title=\"");
        output.Write(name);
        output.Write("\"");
        WriteSize(" width=\"", image.Width, output);
        WriteSize(" height=\"", image.Height, output);
        output.Write(" loading=\"lazy\" referrerpolicy=\"no-referrer\">");
    }

    private static void WriteSize(string attribute, int? value, IBufferWriter<char> output)
    {
        if (value is not { } size)
        {
            return;
        }

        output.Write(attribute);
        output.Write(size.ToString(CultureInfo.InvariantCulture));
        output.Write("\"");
    }
}
