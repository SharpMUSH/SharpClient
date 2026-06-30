namespace SharpClient.Core.Domain;

public sealed class TriggerRule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public TriggerKind Kind { get; set; }
    public string Pattern { get; set; } = string.Empty;
    public TriggerActionKind Action { get; set; }
    public string ActionValue { get; set; } = string.Empty; // e.g. send text, notify template, highlight colour index
    public bool Enabled { get; set; } = true;
}
