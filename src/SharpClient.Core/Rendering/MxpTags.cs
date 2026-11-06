namespace SharpClient.Core.Rendering;

/// <summary>
/// What SharpClient's MXP rendering (<see cref="AnsiParser"/>) acts on, for answering a server's
/// <c>&lt;SUPPORT&gt;</c>. A tag or attribute is listed only when the parser does something with it:
/// claiming more makes a server send markup that is then shown stripped.
/// </summary>
public static class MxpTags
{
    /// <summary>The MXP revision answered to <c>&lt;VERSION&gt;</c>.</summary>
    public const string MxpRevision = "1.0";

    public static IReadOnlyList<string> Supported { get; } = ["send", "image"];

    private static readonly Dictionary<string, string[]> s_attributes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["send"] = ["href", "hint"],
        ["image"] = ["fname", "url", "t"],
    };

    /// <summary>
    /// The answer to a <c>&lt;SUPPORT&gt;</c> naming <paramref name="asked"/>: every supported tag when
    /// it named nothing, otherwise each entry as supported or not. <c>tag.attr</c> is supported when the
    /// parser reads that attribute; <c>tag.*</c> expands to each attribute of a supported tag, as the
    /// specification asks, and is <c>-tag.*</c> for a tag this client does not render.
    /// </summary>
    public static (IReadOnlyList<string> Supported, IReadOnlyList<string> Unsupported) Answer(IReadOnlyList<string> asked)
    {
        if (asked.Count == 0)
        {
            return (Supported, []);
        }

        var supported = new List<string>();
        var unsupported = new List<string>();
        foreach (var entry in asked)
        {
            var name = entry.Trim('"', '\'');
            if (name.Length == 0)
            {
                continue;
            }

            var dot = name.IndexOf('.', StringComparison.Ordinal);
            if (dot < 0)
            {
                (IsSupported(name) ? supported : unsupported).Add(name);
                continue;
            }

            var tag = name[..dot];
            var attribute = name[(dot + 1)..];
            var attributes = IsSupported(tag) && s_attributes.TryGetValue(tag, out var known) ? known : [];
            if (attribute == "*")
            {
                if (IsSupported(tag))
                {
                    supported.AddRange(attributes.Select(a => $"{tag}.{a}"));
                }
                else
                {
                    unsupported.Add(name);
                }
            }
            else
            {
                (attributes.Contains(attribute, StringComparer.OrdinalIgnoreCase) ? supported : unsupported).Add(name);
            }
        }

        return (supported, unsupported);
    }

    private static bool IsSupported(string tag) => Supported.Contains(tag, StringComparer.OrdinalIgnoreCase);
}
