using System.Text;

namespace SharpClient.Core.Rendering;

public static class AnsiParser
{
    private const char Escape = '\e';

    public static IReadOnlyList<StyledSegment> Parse(string line, MxpParserState? mxp = null)
    {
        var segments = new List<StyledSegment>();
        var text = new StringBuilder();
        var style = TextStyle.Default;
        var index = 0;

        // Pending command-link metadata, carried onto every segment flushed while a
        // <SEND>/<a xch_cmd> link is open (so SGR changes inside a link stay clickable).
        string? pendingCommand = null;
        string? pendingHint = null;
        var inSend = false;
        var sendShorthand = false;
        var inAnchor = false;

        void Flush()
        {
            if (text.Length == 0)
            {
                return;
            }

            segments.Add(new StyledSegment(text.ToString(), style)
            {
                Command = pendingCommand,
                Hint = pendingHint,
            });
            text.Clear();
        }

        void HandleTag(string raw)
        {
            var body = raw.Trim();
            var isClose = body.StartsWith('/');
            if (isClose)
            {
                body = body[1..].TrimStart();
            }

            var sp = body.IndexOfAny([' ', '\t']);
            var name = (sp < 0 ? body : body[..sp]).ToLowerInvariant();
            var attrs = sp < 0 ? string.Empty : body[(sp + 1)..];

            if (!isClose)
            {
                if (name == "send" && mxp!.IsMxpActive)
                {
                    // Secure elements are only honoured on a Secure line; otherwise the
                    // tag is stripped and its inner text flows through as plain text.
                    if (mxp.IsSecure)
                    {
                        Flush();
                        var href = GetAttr(attrs, "href");
                        pendingHint = GetAttr(attrs, "hint");
                        if (href is not null)
                        {
                            pendingCommand = href;
                            sendShorthand = false;
                        }
                        else
                        {
                            pendingCommand = null;
                            sendShorthand = true; // inner text becomes the command at </SEND>
                        }

                        inSend = true;
                    }
                }
                else if (name == "a" && mxp.IsPuebloActive)
                {
                    var cmd = GetAttr(attrs, "xch_cmd");
                    if (cmd is not null)
                    {
                        Flush();
                        pendingCommand = cmd;
                        pendingHint = GetAttr(attrs, "xch_hint");
                        inAnchor = true;
                    }
                }
                // Any other tag (or a non-command anchor) is stripped silently.
            }
            else
            {
                if (name == "send" && inSend)
                {
                    if (sendShorthand && pendingCommand is null)
                    {
                        pendingCommand = text.ToString();
                    }

                    Flush();
                    pendingCommand = null;
                    pendingHint = null;
                    inSend = false;
                    sendShorthand = false;
                }
                else if (name == "a" && inAnchor)
                {
                    Flush();
                    pendingCommand = null;
                    pendingHint = null;
                    inAnchor = false;
                }
                // Other closing tags are stripped silently.
            }
        }

        while (index < line.Length)
        {
            var current = line[index];
            if (current == Escape && index + 1 < line.Length && line[index + 1] == '[')
            {
                var end = index + 2;
                while (end < line.Length && !IsCsiFinal(line[end]))
                {
                    end++;
                }

                if (end < line.Length)
                {
                    var final = line[end];
                    var parameters = line.Substring(index + 2, end - (index + 2));
                    if (final == 'm')
                    {
                        Flush();
                        style = ApplySgr(style, parameters);
                    }
                    else if (final == 'z' && mxp is not null && mxp.IsMxpActive)
                    {
                        if (int.TryParse(parameters, out var mode))
                        {
                            mxp.ApplyMode(mode);
                        }
                    }

                    index = end + 1;
                    continue;
                }

                break;
            }

            if (current == Escape)
            {
                index++;
                continue;
            }

            // MXP / Pueblo markup. Locked MXP lines (mode 2) suppress all parsing.
            if (current == '<' && mxp is not null
                && (mxp.IsPuebloActive || (mxp.IsMxpActive && mxp.LineMode != 2)))
            {
                var gt = line.IndexOf('>', index);
                if (gt < 0)
                {
                    text.Append(current); // unterminated tag: treat '<' as literal
                    index++;
                    continue;
                }

                HandleTag(line.Substring(index + 1, gt - (index + 1)));
                index = gt + 1;
                continue;
            }

            text.Append(current);
            index++;
        }

        Flush();
        return segments;
    }

    /// <summary>Extract an attribute value (quoted or unquoted) from a tag body, case-insensitively.</summary>
    private static string? GetAttr(string body, string name)
    {
        var from = 0;
        while (from < body.Length)
        {
            var idx = body.IndexOf(name, from, StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
            {
                return null;
            }

            var atTokenStart = idx == 0 || body[idx - 1] is ' ' or '\t';
            var j = idx + name.Length;
            while (j < body.Length && body[j] is ' ' or '\t')
            {
                j++;
            }

            if (atTokenStart && j < body.Length && body[j] == '=')
            {
                j++;
                while (j < body.Length && body[j] is ' ' or '\t')
                {
                    j++;
                }

                if (j < body.Length && body[j] is '"' or '\'')
                {
                    var quote = body[j];
                    var start = ++j;
                    while (j < body.Length && body[j] != quote)
                    {
                        j++;
                    }

                    return body[start..j];
                }
                else
                {
                    var start = j;
                    while (j < body.Length && body[j] is not (' ' or '\t'))
                    {
                        j++;
                    }

                    return body[start..j];
                }
            }

            from = idx + name.Length;
        }

        return null;
    }

    private static bool IsCsiFinal(char c) => c is >= '@' and <= '~';

    private static TextStyle ApplySgr(TextStyle style, string parameters)
    {
        if (parameters.Length == 0)
        {
            return TextStyle.Default;
        }

        var codes = parameters.Split(';');
        for (var i = 0; i < codes.Length; i++)
        {
            if (!int.TryParse(codes[i], out var code))
            {
                continue;
            }

            switch (code)
            {
                case 0:
                    style = TextStyle.Default;
                    break;
                case 1:
                    style = style with { Bold = true };
                    break;
                case 4:
                    style = style with { Underline = true };
                    break;
                case 7:
                    style = style with { Inverse = true };
                    break;
                case 22:
                    style = style with { Bold = false };
                    break;
                case 24:
                    style = style with { Underline = false };
                    break;
                case 27:
                    style = style with { Inverse = false };
                    break;
                case 39:
                    style = style with { Foreground = AnsiColor.Default };
                    break;
                case 49:
                    style = style with { Background = AnsiColor.Default };
                    break;
                case 38 when TryReadExtended(codes, ref i, out var fg):
                    style = style with { Foreground = fg };
                    break;
                case 48 when TryReadExtended(codes, ref i, out var bg):
                    style = style with { Background = bg };
                    break;
                case >= 30 and <= 37:
                    style = style with { Foreground = AnsiColor.Indexed(code - 30) };
                    break;
                case >= 90 and <= 97:
                    style = style with { Foreground = AnsiColor.Indexed(code - 90 + 8) };
                    break;
                case >= 40 and <= 47:
                    style = style with { Background = AnsiColor.Indexed(code - 40) };
                    break;
                case >= 100 and <= 107:
                    style = style with { Background = AnsiColor.Indexed(code - 100 + 8) };
                    break;
            }
        }

        return style;
    }

    private static bool TryReadExtended(string[] codes, ref int i, out AnsiColor colour)
    {
        colour = AnsiColor.Default;
        if (i + 1 >= codes.Length)
        {
            return false;
        }

        if (codes[i + 1] == "5")
        {
            if (i + 2 < codes.Length && int.TryParse(codes[i + 2], out var n))
            {
                colour = AnsiColor.Indexed(n);
                i += 2;
                return true;
            }

            return false;
        }

        if (codes[i + 1] == "2")
        {
            // Truecolor (24-bit): consume "2" + R + G + B components.
            // Phase-1 scope: graceful degradation — colour stays AnsiColor.Default.
            var remaining = codes.Length - 1 - i;
            i += Math.Min(4, remaining);
            return true;
        }

        return false;
    }
}
