using System.Text;

namespace SharpClient.Core.Rendering;

public static class AnsiParser
{
    private const char Escape = '\e';

    public static IReadOnlyList<StyledSegment> Parse(string line)
    {
        var segments = new List<StyledSegment>();
        var text = new StringBuilder();
        var style = TextStyle.Default;
        var index = 0;

        void Flush()
        {
            if (text.Length == 0)
            {
                return;
            }

            segments.Add(new StyledSegment(text.ToString(), style));
            text.Clear();
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

            text.Append(current);
            index++;
        }

        Flush();
        return segments;
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
