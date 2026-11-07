using SharpClient.Core.Domain;
using MarkupString;

namespace SharpClient.Core.Triggers;

public sealed record TriggerOutcome(
    IReadOnlyList<MarkupText> Lines,
    IReadOnlyList<string> SendCommands,
    IReadOnlyList<string> Notifications);

public interface ITriggerEngine
{
    // Match rules against rawLine (as the server sent it), apply Highlight rules to the decoded lines it
    // made, and collect Send/Notify actions. Disabled rules are ignored.
    public TriggerOutcome Apply(string rawLine, IReadOnlyList<MarkupText> lines, IReadOnlyList<TriggerRule> rules);
}
