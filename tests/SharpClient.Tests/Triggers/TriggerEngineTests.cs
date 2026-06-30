using SharpClient.Core.Domain;
using SharpClient.Core.Rendering;
using SharpClient.Core.Triggers;

namespace SharpClient.Tests.Triggers;

public sealed class TriggerEngineTests
{
    private readonly TriggerEngine _engine = new();

    [Test]
    public async Task NoRulesReturnsAnsiParsedSegmentsAndEmptyActions()
    {
        var outcome = _engine.Apply("hello world", []);

        var expected = AnsiParser.Parse("hello world");
        await Assert.That(outcome.Segments.Count).IsEqualTo(expected.Count);
        await Assert.That(outcome.Segments[0]).IsEqualTo(expected[0]);
        await Assert.That(outcome.SendCommands).IsEmpty();
        await Assert.That(outcome.Notifications).IsEmpty();
    }

    [Test]
    public async Task NoRulesEmptyLineReturnsEmptySegmentsAndEmptyActions()
    {
        var outcome = _engine.Apply(string.Empty, []);

        await Assert.That(outcome.Segments).IsEmpty();
        await Assert.That(outcome.SendCommands).IsEmpty();
        await Assert.That(outcome.Notifications).IsEmpty();
    }

    [Test]
    public async Task SubstringMatchSendIsCollected()
    {
        var rule = new TriggerRule
        {
            Kind = TriggerKind.Substring,
            Pattern = "dragon",
            Action = TriggerActionKind.Send,
            ActionValue = "attack dragon",
        };

        var outcome = _engine.Apply("A dragon appears!", [rule]);

        await Assert.That(outcome.SendCommands).Contains("attack dragon");
        await Assert.That(outcome.Notifications).IsEmpty();
    }

    [Test]
    public async Task SubstringMatchNotifyIsCollected()
    {
        var rule = new TriggerRule
        {
            Kind = TriggerKind.Substring,
            Pattern = "health low",
            Action = TriggerActionKind.Notify,
            ActionValue = "HP critical!",
        };

        var outcome = _engine.Apply("Your health low, be careful.", [rule]);

        await Assert.That(outcome.Notifications).Contains("HP critical!");
        await Assert.That(outcome.SendCommands).IsEmpty();
    }

    [Test]
    public async Task RegexMatchSendIsCollected()
    {
        var rule = new TriggerRule
        {
            Kind = TriggerKind.Regex,
            Pattern = @"\bYou kill\b",
            Action = TriggerActionKind.Send,
            ActionValue = "loot",
        };

        var outcome = _engine.Apply("You kill the goblin.", [rule]);

        await Assert.That(outcome.SendCommands).Contains("loot");
    }

    [Test]
    public async Task RegexNoMatchContributesNothing()
    {
        var rule = new TriggerRule
        {
            Kind = TriggerKind.Regex,
            Pattern = @"^dragon$",
            Action = TriggerActionKind.Send,
            ActionValue = "flee",
        };

        var outcome = _engine.Apply("A dragon appears!", [rule]);

        await Assert.That(outcome.SendCommands).IsEmpty();
    }

    [Test]
    public async Task SubstringNoMatchContributesNothing()
    {
        var rule = new TriggerRule
        {
            Kind = TriggerKind.Substring,
            Pattern = "wyvern",
            Action = TriggerActionKind.Send,
            ActionValue = "flee",
        };

        var outcome = _engine.Apply("A dragon appears!", [rule]);

        await Assert.That(outcome.SendCommands).IsEmpty();
    }

    [Test]
    public async Task DisabledRuleIsIgnored()
    {
        var rule = new TriggerRule
        {
            Kind = TriggerKind.Substring,
            Pattern = "dragon",
            Action = TriggerActionKind.Send,
            ActionValue = "attack",
            Enabled = false,
        };

        var outcome = _engine.Apply("A dragon appears!", [rule]);

        await Assert.That(outcome.SendCommands).IsEmpty();
    }

    [Test]
    public async Task InvalidRegexPatternDoesNotThrowTreatedAsNoMatch()
    {
        var rule = new TriggerRule
        {
            Kind = TriggerKind.Regex,
            Pattern = "[invalid((",
            Action = TriggerActionKind.Send,
            ActionValue = "should not appear",
        };

        var outcome = _engine.Apply("some line", [rule]);

        await Assert.That(outcome.SendCommands).IsEmpty();
    }

    [Test]
    public async Task HighlightRestylesAllSegmentsWithGivenIndex()
    {
        var rule = new TriggerRule
        {
            Kind = TriggerKind.Substring,
            Pattern = "red",
            Action = TriggerActionKind.Highlight,
            ActionValue = "196",
        };

        var outcome = _engine.Apply("the red dragon", [rule]);

        await Assert.That(outcome.Segments).IsNotEmpty();
        foreach (var segment in outcome.Segments)
        {
            await Assert.That(segment.Style.Foreground).IsEqualTo(AnsiColor.Indexed(196));
        }
    }

    [Test]
    public async Task HighlightWithAnsiInputRestylesAllSegments()
    {
        var rule = new TriggerRule
        {
            Kind = TriggerKind.Substring,
            Pattern = "danger",
            Action = TriggerActionKind.Highlight,
            ActionValue = "9",
        };

        // Two segments: "danger" (red fg) and " ahead" (default)
        var outcome = _engine.Apply("\e[31mdanger\e[0m ahead", [rule]);

        await Assert.That(outcome.Segments.Count).IsEqualTo(2);
        await Assert.That(outcome.Segments[0].Style.Foreground).IsEqualTo(AnsiColor.Indexed(9));
        await Assert.That(outcome.Segments[1].Style.Foreground).IsEqualTo(AnsiColor.Indexed(9));
    }

    [Test]
    public async Task HighlightInvalidActionValueDoesNotApplyHighlight()
    {
        var rule = new TriggerRule
        {
            Kind = TriggerKind.Substring,
            Pattern = "hello",
            Action = TriggerActionKind.Highlight,
            ActionValue = "not-a-number",
        };

        var outcome = _engine.Apply("hello world", [rule]);

        await Assert.That(outcome.Segments[0].Style.Foreground).IsEqualTo(AnsiColor.Default);
    }

    [Test]
    public async Task MultipleMatchingRulesAccumulate()
    {
        var sendRule = new TriggerRule
        {
            Kind = TriggerKind.Substring,
            Pattern = "dragon",
            Action = TriggerActionKind.Send,
            ActionValue = "attack",
        };
        var notifyRule = new TriggerRule
        {
            Kind = TriggerKind.Substring,
            Pattern = "dragon",
            Action = TriggerActionKind.Notify,
            ActionValue = "Dragon spotted!",
        };

        var outcome = _engine.Apply("The dragon roars.", [sendRule, notifyRule]);

        await Assert.That(outcome.SendCommands).Contains("attack");
        await Assert.That(outcome.Notifications).Contains("Dragon spotted!");
    }

    [Test]
    public async Task LaterHighlightRuleOverridesEarlierOne()
    {
        var first = new TriggerRule
        {
            Kind = TriggerKind.Substring,
            Pattern = "dragon",
            Action = TriggerActionKind.Highlight,
            ActionValue = "1",
        };
        var second = new TriggerRule
        {
            Kind = TriggerKind.Substring,
            Pattern = "dragon",
            Action = TriggerActionKind.Highlight,
            ActionValue = "9",
        };

        var outcome = _engine.Apply("The dragon roars.", [first, second]);

        foreach (var segment in outcome.Segments)
        {
            await Assert.That(segment.Style.Foreground).IsEqualTo(AnsiColor.Indexed(9));
        }
    }

    [Test]
    public async Task MultipleSendCommandsAllCollected()
    {
        var rule1 = new TriggerRule
        {
            Kind = TriggerKind.Substring,
            Pattern = "dragon",
            Action = TriggerActionKind.Send,
            ActionValue = "attack",
        };
        var rule2 = new TriggerRule
        {
            Kind = TriggerKind.Substring,
            Pattern = "dragon",
            Action = TriggerActionKind.Send,
            ActionValue = "loot",
        };

        var outcome = _engine.Apply("The dragon dies.", [rule1, rule2]);

        await Assert.That(outcome.SendCommands.Count).IsEqualTo(2);
        await Assert.That(outcome.SendCommands).Contains("attack");
        await Assert.That(outcome.SendCommands).Contains("loot");
    }
}
