using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace SharpClient.Core.Diagnostics;

/// <summary>Reverses <see cref="FileLogStore"/>'s line format so the log can be shown in the app.</summary>
public static partial class LogEntryParser
{
    private const string TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff zzz";

    [GeneratedRegex(
        @"^(?<ts>\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3} [+-]\d{2}:\d{2}) \[(?<level>[^\]]+)\] (?<rest>.*)$")]
    private static partial Regex HeaderPattern();

    // A category is a type name, so it never contains whitespace. Requiring that keeps a message
    // like "NAWS fit: cols=78" from being split into a bogus category.
    [GeneratedRegex(@"^(?<category>[^\s:]+): (?<message>.*)$")]
    private static partial Regex CategoryPattern();

    public static IReadOnlyList<LogEntry> Parse(string text)
    {
        var entries = new List<LogEntry>();
        if (string.IsNullOrEmpty(text))
        {
            return entries;
        }

        DateTimeOffset timestamp = default;
        var level = string.Empty;
        var category = string.Empty;
        var message = string.Empty;
        StringBuilder? detail = null;
        var open = false;

        foreach (var raw in text.Replace("\r\n", "\n").Split('\n'))
        {
            var header = HeaderPattern().Match(raw);
            if (header.Success)
            {
                if (!DateTimeOffset.TryParseExact(
                    header.Groups["ts"].Value, TimestampFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedTimestamp))
                {
                    // Skip lines that match the header pattern but have invalid timestamps (corruption).
                    continue;
                }

                if (open)
                {
                    entries.Add(Build(timestamp, level, category, message, detail));
                }

                timestamp = parsedTimestamp;
                level = header.Groups["level"].Value;
                var rest = header.Groups["rest"].Value;
                var split = CategoryPattern().Match(rest);
                category = split.Success ? split.Groups["category"].Value : string.Empty;
                message = split.Success ? split.Groups["message"].Value : rest;
                detail = null;
                open = true;
                continue;
            }

            // Lines before the first header are the tail of an entry that rotation cut in half.
            if (!open || raw.Length == 0)
            {
                continue;
            }

            detail ??= new StringBuilder();
            if (detail.Length > 0)
            {
                detail.Append('\n');
            }

            detail.Append(raw);
        }

        if (open)
        {
            entries.Add(Build(timestamp, level, category, message, detail));
        }

        return entries;
    }

    private static LogEntry Build(
        DateTimeOffset timestamp, string level, string category, string message, StringBuilder? detail)
        => new(timestamp, level, category, message, detail?.ToString());
}
