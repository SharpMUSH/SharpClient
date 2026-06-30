namespace SharpClient.Core.Rendering;

public readonly record struct StyledSegment(string Text, TextStyle Style)
{
    /// <summary>
    /// If non-null, this segment is a clickable command-link (MXP &lt;SEND&gt; or Pueblo
    /// &lt;a xch_cmd&gt;). Clicking it sends this string to the server. Null for normal text.
    /// </summary>
    public string? Command { get; init; }

    /// <summary>Optional tooltip for a command link. Null if unspecified.</summary>
    public string? Hint { get; init; }
}
