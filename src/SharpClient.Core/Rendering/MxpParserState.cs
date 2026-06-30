namespace SharpClient.Core.Rendering;

/// <summary>
/// Per-session markup-negotiation state for MXP (telnet option 91) and Pueblo.
/// Passed to <see cref="AnsiParser.Parse(string, MxpParserState?)"/> on each line so the
/// parser knows whether clickable command-links are permitted on the current line.
/// </summary>
public sealed class MxpParserState
{
    /// <summary>Whether MXP telnet option 91 was successfully negotiated.</summary>
    public bool IsMxpActive { get; set; }

    /// <summary>Whether the Pueblo banner was seen and the PUEBLOCLIENT handshake sent.</summary>
    public bool IsPuebloActive { get; set; }

    /// <summary>
    /// Current MXP line mode. Per-line modes (0=Open,1=Secure,2=Locked,3=Reset) revert to
    /// <see cref="DefaultLineMode"/> at each newline; lock modes (5,6) change the default.
    /// </summary>
    public int LineMode { get; set; }

    /// <summary>Persistent default mode — 0 (Open) or 1 (Secure, via Lock-Secure).</summary>
    public int DefaultLineMode { get; set; }

    /// <summary>Reset the transient line mode to the persistent default. Call at line start.</summary>
    public void BeginLine() => LineMode = DefaultLineMode;

    /// <summary>Apply an ESC[#z mode sequence.</summary>
    public void ApplyMode(int mode)
    {
        switch (mode)
        {
            case 0 or 1 or 2:
                LineMode = mode;
                break;
            case 3: // Reset: close tags, revert to Open for this line
                LineMode = 0;
                break;
            case 5: // Lock Open: Open becomes the persistent default
                LineMode = 0;
                DefaultLineMode = 0;
                break;
            case 6: // Lock Secure: Secure becomes the persistent default
                LineMode = 1;
                DefaultLineMode = 1;
                break;
        }
    }

    /// <summary>True when the current line permits Secure elements (SEND / A).</summary>
    public bool IsSecure => LineMode == 1;
}
