using SharpClient.Core.Domain;
using SharpClient.Core.Rendering;
using SharpClient.Tests.Rendering;
using SharpClient.Core.Triggers;

namespace SharpClient.Tests.Triggers;

public sealed class TriggerEngineTests
{
    private readonly TriggerEngine _engine = new();

    private static TriggerOutcome Apply(TriggerEngine engine, string raw, IReadOnlyList<TriggerRule> rules) =>
        engine.Apply(raw, new OutputDecoder().Decode(raw), rules);

    private static IEnumerable<MarkupString.Display.DisplaySpan> Spans(TriggerOutcome outcome) =>
        outcome.Lines.SelectMany(line => line.Spans());

    private static string Hex(int index) => MarkupString.Display.SpanRenderer.Hex(OutputPalette.Instance.Entry(index));

    [Test]
    public async Task NoRulesReturnsTheDecodedLinesAndEmptyActions()
    {
        var decoded = new OutputDecoder().Decode("hello world");
        var outcome = _engine.Apply("hello world", decoded, []);

        await Assert.That(outcome.Lines).IsEqualTo(decoded);
        await Assert.That(outcome.SendCommands).IsEmpty();
        await Assert.That(outcome.Notifications).IsEmpty();
    }

    [Test]
    public async Task NoRulesEmptyLineReturnsOneEmptyLineAndEmptyActions()
    {
        var outcome = Apply(_engine, string.Empty, []);

        await Assert.That(outcome.Lines.Count).IsEqualTo(1);
        await Assert.That(Spans(outcome)).IsEmpty();
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

        var outcome = Apply(_engine, "A dragon appears!", [rule]);

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

        var outcome = Apply(_engine, "Your health low, be careful.", [rule]);

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

        var outcome = Apply(_engine, "You kill the goblin.", [rule]);

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

        var outcome = Apply(_engine, "A dragon appears!", [rule]);

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

        var outcome = Apply(_engine, "A dragon appears!", [rule]);

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

        var outcome = Apply(_engine, "A dragon appears!", [rule]);

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

        var outcome = Apply(_engine, "some line", [rule]);

        await Assert.That(outcome.SendCommands).IsEmpty();
    }

    [Test]
    public async Task HighlightRestylesAllSpansWithGivenIndex()
    {
        var rule = new TriggerRule
        {
            Kind = TriggerKind.Substring,
            Pattern = "red",
            Action = TriggerActionKind.Highlight,
            ActionValue = "196",
        };

        var outcome = Apply(_engine, "the red dragon", [rule]);

        await Assert.That(Spans(outcome)).IsNotEmpty();
        foreach (var segment in Spans(outcome))
        {
            await Assert.That(segment.Color()).IsEqualTo(Hex(196));
        }
    }

    [Test]
    public async Task HighlightWithAnsiInputRestylesAllSpans()
    {
        var rule = new TriggerRule
        {
            Kind = TriggerKind.Substring,
            Pattern = "danger",
            Action = TriggerActionKind.Highlight,
            ActionValue = "9",
        };

        // "danger" is red and " ahead" default; the highlight makes both one colour, so one span.
        var outcome = Apply(_engine, "\e[31mdanger\e[0m ahead", [rule]);

        var spans = Spans(outcome).ToList();
        await Assert.That(spans.Select(s => s.Text)).IsEquivalentTo(["danger ahead"]);
        await Assert.That(spans[0].Color()).IsEqualTo(Hex(9));
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

        var outcome = Apply(_engine, "hello world", [rule]);

        await Assert.That(Spans(outcome).Single().Color()).IsNull();
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

        var outcome = Apply(_engine, "The dragon roars.", [sendRule, notifyRule]);

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

        var outcome = Apply(_engine, "The dragon roars.", [first, second]);

        foreach (var segment in Spans(outcome))
        {
            await Assert.That(segment.Color()).IsEqualTo(Hex(9));
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

        var outcome = Apply(_engine, "The dragon dies.", [rule1, rule2]);

        await Assert.That(outcome.SendCommands.Count).IsEqualTo(2);
        await Assert.That(outcome.SendCommands).Contains("attack");
        await Assert.That(outcome.SendCommands).Contains("loot");
    }

    [Test]
    public async Task HighlightOutOfRangeActionValueSkippedFirstColourKept()
    {
        // First rule: valid index 3 → should apply.
        // Second rule: index 256 (out-of-range 0-255) → must be skipped; first colour stays.
        var first = new TriggerRule
        {
            Kind = TriggerKind.Substring,
            Pattern = "hero",
            Action = TriggerActionKind.Highlight,
            ActionValue = "3",
        };
        var second = new TriggerRule
        {
            Kind = TriggerKind.Substring,
            Pattern = "hero",
            Action = TriggerActionKind.Highlight,
            ActionValue = "256", // out of range — skipped
        };

        var outcome = Apply(_engine, "brave hero stands", [first, second]);

        await Assert.That(Spans(outcome)).IsNotEmpty();
        foreach (var segment in Spans(outcome))
        {
            await Assert.That(segment.Color()).IsEqualTo(Hex(3));
        }
    }

    [Test]
    public async Task HighlightOnlyOutOfRangeActionValueLeavesSpansDefault()
    {
        var rule = new TriggerRule
        {
            Kind = TriggerKind.Substring,
            Pattern = "hero",
            Action = TriggerActionKind.Highlight,
            ActionValue = "256", // out of range — no highlight applied
        };

        var outcome = Apply(_engine, "brave hero stands", [rule]);

        await Assert.That(Spans(outcome)).IsNotEmpty();
        foreach (var segment in Spans(outcome))
        {
            await Assert.That(segment.Color()).IsNull();
        }
    }
}
