namespace SharpClient.Core.Domain;

public sealed class World
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; }
    public List<Character> Characters { get; set; } = [];
    public List<TriggerRule> Triggers { get; set; } = [];   // world-scope
    public List<AliasRule> Aliases { get; set; } = [];      // world-scope
}
