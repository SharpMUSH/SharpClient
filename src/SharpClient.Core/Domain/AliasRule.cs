namespace SharpClient.Core.Domain;

public sealed class AliasRule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Pattern { get; set; } = string.Empty;     // e.g. "^k (.+)$"
    public string Expansion { get; set; } = string.Empty;   // e.g. "kill $1"
    public bool Enabled { get; set; } = true;
}
