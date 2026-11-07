using MarkupString;
using MarkupString.Ansi.Decoding;
using MarkupString.Decoding;
using MarkupString.Display;
using MarkupString.Mxp.Decoding;
using MarkupString.Pueblo.Decoding;

namespace SharpClient.Core.Rendering;

/// <summary>
/// One connection's decoder: turns each line the telnet layer delivers into <see cref="MarkupText"/>
/// lines, keeping the style, MXP line mode and open elements from line to line. Calls are serialised:
/// the read loop decodes while negotiation and state changes arrive on other threads.
/// </summary>
public sealed class OutputDecoder
{
    /// <summary>ANSI/VT, MXP and Pueblo. MXP and Pueblo stay off until telnet negotiates them.</summary>
    public static DecoderRegistry Registry { get; } = DecoderRegistry.Empty.WithVt().WithMxp().WithPueblo();

    private readonly Lock _lock = new();
    private readonly Decoder _decoder = Registry.CreateDecoder(new DecoderOptions
    {
        // The telnet layer answers MXP <SUPPORT>/<VERSION>, and there is no path for VT replies yet,
        // so the decoder answers nothing.
        Identity = new ClientIdentity { Name = "SharpClient", Answers = ClientAnswers.None },
    });

    /// <summary>The text protocol that is on: <c>mxp</c>, <c>pueblo</c>, or null.</summary>
    public string? ActiveProtocol
    {
        get
        {
            lock (_lock)
            {
                return _decoder.ActiveProtocol;
            }
        }
    }

    /// <summary>MXP was negotiated (telnet option 91).</summary>
    public void EnableMxp()
    {
        lock (_lock)
        {
            _decoder.EnableProtocol(MxpDecoding.Protocol);
        }
    }

    /// <summary>The server switched the connection to Pueblo.</summary>
    public void EnablePueblo()
    {
        lock (_lock)
        {
            _decoder.EnableProtocol(PuebloDecoding.Protocol);
        }
    }

    /// <summary>The connection ended: forget the style, protocols and anything half-read.</summary>
    public void Reset()
    {
        lock (_lock)
        {
            _decoder.Reset();
        }
    }

    /// <summary>
    /// The lines one telnet line makes. Usually one; an MXP <c>&lt;BR&gt;</c> or Pueblo <c>&lt;br&gt;</c> makes
    /// more. A telnet line that only closed a line already ended by <c>&lt;br&gt;</c> adds no blank line.
    /// </summary>
    public IReadOnlyList<MarkupText> Decode(string raw)
    {
        lock (_lock)
        {
            return DecodeLocked(raw);
        }
    }

    private List<MarkupText> DecodeLocked(string raw)
    {
        var fed = _decoder.Feed(raw);
        var lines = new List<MarkupText>(fed.Count + 1);
        foreach (var line in fed)
        {
            lines.Add(line.Content);
        }

        if (_decoder.HasPendingLine || lines.Count == 0)
        {
            lines.Add(_decoder.EndLine().Content);
        }

        return lines;
    }
}

/// <summary>How SharpClient's output view draws a decoded line.</summary>
public static class OutputDisplay
{
    /// <summary>ANSI styles and the shared vocabulary (pictures, menus), then trigger highlights.</summary>
    public static DisplayRegistry Styles { get; } = DisplayRegistry.Core.WithAnsi().With(TriggerHighlightStyles.Instance);

    /// <summary>
    /// A browser that opens links, sends commands and shows menus and pictures. Prompt links become
    /// command links (there is no prompt-into-input yet), and sound and panes are not drawn.
    /// </summary>
    public static DisplayOptions Options { get; } = new()
    {
        Capabilities = OutputCapabilities.Browser with
        {
            Interactions = InteractionKinds.Url | InteractionKinds.Command | InteractionKinds.Menu,
            Sound = false,
            Panes = false,
        },
        Colors = OutputPalette.Instance,
        DefaultForeground = OutputPalette.Phosphor,
        DefaultBackground = OutputPalette.Background,
    };

    public static IReadOnlyList<DisplaySpan> Render(MarkupText content) =>
        SpanRenderer.Render(content, Styles, Options);
}
