namespace SharpClient.Core.Formatting;

public enum PosePrefix
{
    Say,
    Pose,
    Semipose,
    Emit,
    Custom,
}

/// <summary>
/// Turns multi-line prose into the single line a MUSH expects: literal percent signs are doubled so
/// the server does not treat them as substitutions, and line breaks become <c>%r</c>.
/// </summary>
public static class MushPoseFormatter
{
    public static string CommandFor(PosePrefix prefix, string customPrefix) => prefix switch
    {
        PosePrefix.Say => "say",
        PosePrefix.Pose => "pose",
        PosePrefix.Semipose => "semipose",
        PosePrefix.Emit => "@emit",
        _ => customPrefix,
    };

    public static string Format(string prefix, string body) => Join(prefix, EscapeBody(body));

    private static string EscapeBody(string body)
    {
        var lines = body.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

        var first = 0;
        var last = lines.Length - 1;
        while (first <= last && lines[first].Trim().Length == 0)
        {
            first++;
        }

        while (last >= first && lines[last].Trim().Length == 0)
        {
            last--;
        }

        if (first > last)
        {
            return string.Empty;
        }

        // Escape per line, then join with %r: the substitution markers must not be escaped themselves.
        var escaped = new string[last - first + 1];
        for (var i = first; i <= last; i++)
        {
            escaped[i - first] = lines[i].TrimEnd().Replace("%", "%%");
        }

        return string.Join("%r", escaped);
    }

    private static string Join(string prefix, string body)
    {
        if (prefix.Length == 0)
        {
            return body;
        }

        if (body.Length == 0)
        {
            return prefix.TrimEnd();
        }

        var last = prefix[^1];
        return last is '=' or '/' || char.IsWhiteSpace(last)
            ? prefix + body
            : prefix + " " + body;
    }
}
