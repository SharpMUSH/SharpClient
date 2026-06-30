using System.Text.RegularExpressions;
using SharpClient.Core.Domain;

namespace SharpClient.Core.Triggers;

public sealed class AliasEngine : IAliasEngine
{
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(100);

    public string Expand(string input, IReadOnlyList<AliasRule> aliases)
    {
        foreach (var alias in aliases)
        {
            if (!alias.Enabled)
            {
                continue;
            }

            Match match;
            try
            {
                match = Regex.Match(input, alias.Pattern, RegexOptions.None, MatchTimeout);
            }
            catch (Exception)
            {
                // Invalid pattern or timeout — skip this alias rather than propagating.
                continue;
            }

            if (!match.Success)
            {
                continue;
            }

            // Substitute $0 (whole match) and $1..$9 (capture groups).
            // Iterate high-to-low so that e.g. "$9" is replaced before "$" could be
            // confused by a shorter prefix. Unmatched group index → empty string.
            var expansion = alias.Expansion;
            for (var i = 9; i >= 0; i--)
            {
                var groupValue = i < match.Groups.Count ? match.Groups[i].Value : string.Empty;
                expansion = expansion.Replace($"${i}", groupValue);
            }

            return expansion;
        }

        return input;
    }
}
