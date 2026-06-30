using System.Text.RegularExpressions;
using SharpClient.Core.Domain;
using SharpClient.Core.Rendering;

namespace SharpClient.Core.Triggers;

public sealed class TriggerEngine : ITriggerEngine
{
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(100);

    public TriggerOutcome Apply(string rawLine, IReadOnlyList<TriggerRule> rules)
    {
        IReadOnlyList<StyledSegment> segments = AnsiParser.Parse(rawLine);
        var sendCommands = new List<string>();
        var notifications = new List<string>();
        AnsiColor? highlightForeground = null;

        foreach (var rule in rules)
        {
            if (!rule.Enabled)
            {
                continue;
            }

            if (!Matches(rawLine, rule))
            {
                continue;
            }

            switch (rule.Action)
            {
                case TriggerActionKind.Send:
                    sendCommands.Add(rule.ActionValue);
                    break;
                case TriggerActionKind.Notify:
                    notifications.Add(rule.ActionValue);
                    break;
                case TriggerActionKind.Highlight:
                    // v1: whole-line highlight simplification — apply the highlight foreground to every
                    // segment regardless of where the match occurred. Per-segment span highlighting is
                    // deferred to a future phase. Later Highlight rules override earlier ones.
                    if (int.TryParse(rule.ActionValue, out var index) && index is >= 0 and <= 255)
                    {
                        highlightForeground = AnsiColor.Indexed(index);
                    }
                    break;
            }
        }

        if (highlightForeground.HasValue)
        {
            var fg = highlightForeground.Value;
            segments = segments.Select(s => s with { Style = s.Style with { Foreground = fg } }).ToList();
        }

        return new TriggerOutcome(segments, sendCommands, notifications);
    }

    private static bool Matches(string rawLine, TriggerRule rule)
    {
        return rule.Kind switch
        {
            TriggerKind.Substring => rawLine.Contains(rule.Pattern, StringComparison.Ordinal),
            TriggerKind.Regex => TryRegexMatch(rawLine, rule.Pattern),
            _ => false,
        };
    }

    private static bool TryRegexMatch(string input, string pattern)
    {
        try
        {
            return Regex.IsMatch(input, pattern, RegexOptions.None, MatchTimeout);
        }
        catch (Exception)
        {
            // Invalid pattern or timeout — treat as non-match rather than propagating.
            return false;
        }
    }
}
