using MarkupString;

namespace SharpClient.Core.Rendering;

/// <summary>
/// Which server links the output view makes clickable: <see cref="UrlPolicy.Safe"/>, except that an address
/// holding a control character, or starting or ending with a space, is refused. A browser removes tabs and newlines from a URL, and spaces
/// and controls around it, so <c>java&amp;#9;script:</c> passes as a relative address and is run as
/// <c>javascript:</c> when clicked.
/// </summary>
public sealed class OutputLinks : IUrlPolicy
{
    /// <summary>The one instance.</summary>
    public static OutputLinks Instance { get; } = new();

    private OutputLinks()
    {
    }

    /// <inheritdoc/>
    public bool IsAllowed(string url) =>
        url is not null
        && !url.AsSpan().ContainsAnyInRange('\0', '\u001f')
        && !url.AsSpan().ContainsAnyInRange('\u007f', '\u009f')
        && url.Trim(' ').Length == url.Length
        && UrlPolicy.Safe.IsAllowed(url);
}
