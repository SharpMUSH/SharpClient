using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using SharpClient.Core.Domain;
using SharpClient.Core.Rendering;

namespace SharpClient.Core.Triggers;

public sealed class TriggerEngine : ITriggerEngine
{
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(100);

    // Compiled-regex cache keyed by (pattern, options).  Invalid patterns are never
    // inserted (the factory throws and GetOrAdd propagates the exception, leaving the
    // slot empty) so the catch block in TryRegexMatch remains the only error path.
    private static readonly ConcurrentDictionary<(string Pattern, RegexOptions Options), Regex>
        RegexCache = new();

    public TriggerOutcome Apply(string rawLine, IReadOnlyList<TriggerRule> rules, MxpParserState? mxp = null)
    {
        IReadOnlyList<StyledSegment> segments = AnsiParser.Parse(rawLine, mxp);
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
            var regex = RegexCache.GetOrAdd(
                (pattern, RegexOptions.None),
                static key => new Regex(key.Pattern, key.Options, MatchTimeout));
            return regex.IsMatch(input);
        }
        catch (Exception)
        {
            // Invalid pattern, compilation failure, or match timeout — treat as non-match
            // rather than propagating. The entry is not inserted into the cache when the
            // factory throws, so each invalid pattern re-attempts compilation on every call
            // (acceptable: invalid patterns are rare and compilation is fast to fail).
            return false;
        }
    }
}
