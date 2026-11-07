namespace SharpClient.Core.Rendering;

/// <summary>
/// Turns MXP and Pueblo image references into a URL the output view may load. Only http, https and
/// data:image are accepted: any other scheme (javascript:, file:, content:) would be the world choosing
/// what the client opens.
/// </summary>
public static class ImageSource
{
    private const int MaxNameLength = 48;

    /// <summary>
    /// The address an image the decoder read (MXP <c>&lt;IMAGE&gt;</c>, Pueblo <c>&lt;img&gt;</c>) may be loaded
    /// from: an absolute http(s) URL, normalised, or a data:image URI. Null for anything else, including a
    /// bare file name, which refers to a local media directory this client does not have.
    /// </summary>
    public static string? Resolve(string source)
    {
        var value = source.Trim();
        return IsDataImage(value) ? value : Absolute(value);
    }

    /// <summary>What to show in place of the picture: the last path segment of its name, bounded.</summary>
    public static string DisplayName(string? fileName)
    {
        var name = fileName?.Trim() ?? string.Empty;
        if (name.Length == 0 || name.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            return "image";
        }

        var query = name.IndexOfAny(['?', '#']);
        if (query >= 0)
        {
            name = name[..query];
        }

        var slash = name.TrimEnd('/').LastIndexOf('/');
        if (slash >= 0)
        {
            name = name[(slash + 1)..].TrimEnd('/');
        }

        if (name.Length == 0)
        {
            return "image";
        }

        return name.Length <= MaxNameLength ? name : name[..(MaxNameLength - 1)] + "…";
    }

    private static bool IsDataImage(string value) =>
        value.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase);

    private static string? Absolute(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && IsWeb(uri) ? uri.AbsoluteUri : null;

    private static bool IsWeb(Uri uri) => uri.Scheme is "http" or "https";
}
