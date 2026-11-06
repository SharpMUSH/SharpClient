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
    /// MXP <c>&lt;IMAGE&gt;</c>: "The classname is appended to the URL, along with the name of the
    /// graphics file itself", so <c>URL=http://h/img/ T=maps map.png</c> is <c>http://h/img/maps/map.png</c>.
    /// Without <c>URL=</c> the name must itself be absolute; a bare file name refers to a local media
    /// directory this client does not have.
    /// </summary>
    public static string? ResolveMxp(string fileName, string? url, string? type)
    {
        var name = fileName.Trim();
        if (name.Length == 0)
        {
            return null;
        }

        if (IsDataImage(name))
        {
            return string.IsNullOrWhiteSpace(url) ? name : null;
        }

        if (string.IsNullOrWhiteSpace(url))
        {
            return Absolute(name);
        }

        var basePath = EnsureSlash(url.Trim());
        if (!string.IsNullOrWhiteSpace(type))
        {
            basePath = EnsureSlash(basePath + type.Trim().Trim('/'));
        }

        if (!Uri.TryCreate(basePath, UriKind.Absolute, out var baseUri) || !IsWeb(baseUri))
        {
            return null;
        }

        return Uri.TryCreate(baseUri, name, out var full) && IsWeb(full) ? full.AbsoluteUri : null;
    }

    /// <summary>Pueblo <c>&lt;img src&gt;</c>, which is plain HTML: an absolute URL or a data:image URI.</summary>
    public static string? ResolvePueblo(string src)
    {
        var value = src.Trim();
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

    private static string EnsureSlash(string value) => value.EndsWith('/') ? value : value + "/";

    private static bool IsWeb(Uri uri) => uri.Scheme is "http" or "https";
}
