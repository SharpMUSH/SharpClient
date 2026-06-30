namespace SharpClient.Core.Domain;

public sealed class Character
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorldId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ConnectSecretKey { get; set; }           // key into ISecretStore, NOT the secret
    public List<TriggerRule> Triggers { get; set; } = [];   // character-scope (override world)
    public List<AliasRule> Aliases { get; set; } = [];
}
