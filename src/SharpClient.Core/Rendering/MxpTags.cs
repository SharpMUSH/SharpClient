namespace SharpClient.Core.Rendering;

/// <summary>
/// What SharpClient's MXP rendering (<see cref="OutputDecoder"/> and the output view) acts on, for answering
/// a server's <c>&lt;SUPPORT&gt;</c>. A tag or attribute is listed only when the client does something with it:
/// claiming more makes a server send markup that is then shown stripped. Sound, frames, variables and
/// relocation are decoded but not acted on, so they are not claimed.
/// </summary>
public static class MxpTags
{
    /// <summary>The MXP revision answered to <c>&lt;VERSION&gt;</c>.</summary>
    public const string MxpRevision = "1.0";

    // Each rendered tag with the attributes the client reads from it. SEND's PROMPT is not claimed:
    // a prompt link is sent as a command, since there is no prompt-into-input yet.
    private static readonly (string Tag, string[] Attributes)[] s_rendered =
    [
        ("b", []), ("bold", []), ("strong", []),
        ("i", []), ("italic", []), ("em", []),
        ("u", []), ("underline", []),
        ("s", []), ("strikeout", []),
        ("h", []), ("high", []),
        ("c", ["fore", "back"]), ("color", ["fore", "back"]),
        ("font", ["color", "back"]),
        ("br", []), ("sbr", []), ("p", []), ("nobr", []),
        ("a", ["href", "hint"]),
        ("send", ["href", "hint"]),
        ("image", ["fname", "url", "w", "h"]),
        ("element", []), ("entity", []), ("attlist", []), ("tag", []),
    ];

    public static IReadOnlyList<string> Supported { get; } = [.. s_rendered.Select(r => r.Tag)];

    private static readonly Dictionary<string, string[]> s_attributes =
        s_rendered.ToDictionary(r => r.Tag, r => r.Attributes, StringComparer.OrdinalIgnoreCase);

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
            var tagSupported = s_attributes.TryGetValue(tag, out var known);
            var attributes = known ?? Array.Empty<string>();
            if (attribute == "*")
            {
                if (tagSupported)
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

    private static bool IsSupported(string tag) => s_attributes.ContainsKey(tag);
}
