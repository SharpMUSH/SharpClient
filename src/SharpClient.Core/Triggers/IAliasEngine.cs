using SharpClient.Core.Domain;

namespace SharpClient.Core.Triggers;

public interface IAliasEngine
{
    // First enabled alias whose Pattern (regex) matches `input` produces its Expansion
    // with $1..$n substituted from capture groups; no match returns input unchanged.
    public string Expand(string input, IReadOnlyList<AliasRule> aliases);
}
