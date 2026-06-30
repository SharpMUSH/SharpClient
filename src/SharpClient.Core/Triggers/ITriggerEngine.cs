using SharpClient.Core.Domain;
using SharpClient.Core.Rendering;

namespace SharpClient.Core.Triggers;

public sealed record TriggerOutcome(
    IReadOnlyList<StyledSegment> Segments,
    IReadOnlyList<string> SendCommands,
    IReadOnlyList<string> Notifications);

public interface ITriggerEngine
{
    // Parse rawLine via AnsiParser (with optional MXP/Pueblo markup state), apply Highlight
    // rules (restyle matched runs), collect Send/Notify actions. Disabled rules are ignored.
    public TriggerOutcome Apply(string rawLine, IReadOnlyList<TriggerRule> rules, MxpParserState? mxp = null);
}
