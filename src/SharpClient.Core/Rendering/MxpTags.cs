using MarkupString.Mxp.Decoding;

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
        ("image", ["fname", "url", "t", "w", "h"]),
        ("element", []), ("entity", []), ("attlist", []), ("tag", []),
    ];

    /// <summary>
    /// The MXP options SharpClient decodes with. <see cref="MxpOptions.Supports"/> holds the tags above, so
    /// the decoder and the <c>&lt;SUPPORT&gt;</c> answer agree.
    /// </summary>
    public static MxpOptions Options { get; } = new()
    {
        Supports = s_rendered.ToDictionary(r => r.Tag, r => (IReadOnlyList<string>)r.Attributes, StringComparer.OrdinalIgnoreCase),
    };

    public static IReadOnlyList<string> Supported { get; } = [.. s_rendered.Select(r => r.Tag)];

    /// <summary>
    /// The answer to a <c>&lt;SUPPORT&gt;</c> naming <paramref name="asked"/>: every supported tag when it
    /// named nothing, otherwise each entry as supported or not (<see cref="MxpOptions.Answer"/>).
    /// </summary>
    public static (IReadOnlyList<string> Supported, IReadOnlyList<string> Unsupported) Answer(IReadOnlyList<string> asked) =>
        Options.Answer(asked);
}
