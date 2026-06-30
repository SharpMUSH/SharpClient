using SharpClient.Core.Domain;
using SharpClient.Core.Triggers;

namespace SharpClient.Tests.Triggers;

public sealed class AliasEngineTests
{
    private readonly AliasEngine _engine = new();

    [Test]
    public async Task BasicCaptureGroupSubstituted()
    {
        var alias = new AliasRule { Pattern = @"^k (.+)$", Expansion = "kill $1" };

        var result = _engine.Expand("k dragon", [alias]);

        await Assert.That(result).IsEqualTo("kill dragon");
    }

    [Test]
    public async Task NoMatchInputReturnedUnchanged()
    {
        var alias = new AliasRule { Pattern = @"^k (.+)$", Expansion = "kill $1" };

        var result = _engine.Expand("go north", [alias]);

        await Assert.That(result).IsEqualTo("go north");
    }

    [Test]
    public async Task NoAliasesInputReturnedUnchanged()
    {
        var result = _engine.Expand("go north", []);

        await Assert.That(result).IsEqualTo("go north");
    }

    [Test]
    public async Task DisabledAliasIsSkipped()
    {
        var alias = new AliasRule { Pattern = @"^k (.+)$", Expansion = "kill $1", Enabled = false };

        var result = _engine.Expand("k dragon", [alias]);

        await Assert.That(result).IsEqualTo("k dragon");
    }

    [Test]
    public async Task FirstMatchWinsSecondAliasIgnored()
    {
        var first = new AliasRule { Pattern = @"^k (.+)$", Expansion = "kill $1" };
        var second = new AliasRule { Pattern = @"^k (.+)$", Expansion = "slay $1" };

        var result = _engine.Expand("k dragon", [first, second]);

        await Assert.That(result).IsEqualTo("kill dragon");
    }

    [Test]
    public async Task DollarZeroSubstitutesWholeMatch()
    {
        var alias = new AliasRule { Pattern = @"^echo (.+)$", Expansion = "say [$0]" };

        var result = _engine.Expand("echo hello", [alias]);

        await Assert.That(result).IsEqualTo("say [echo hello]");
    }

    [Test]
    public async Task InvalidRegexPatternSkippedNoThrow()
    {
        var bad = new AliasRule { Pattern = "[invalid((", Expansion = "boom" };
        var good = new AliasRule { Pattern = @"^k (.+)$", Expansion = "kill $1" };

        var result = _engine.Expand("k goblin", [bad, good]);

        await Assert.That(result).IsEqualTo("kill goblin");
    }

    [Test]
    public async Task InvalidRegexOnlyInputReturnedUnchangedNoThrow()
    {
        var alias = new AliasRule { Pattern = "[invalid((", Expansion = "boom" };

        var result = _engine.Expand("k goblin", [alias]);

        await Assert.That(result).IsEqualTo("k goblin");
    }

    [Test]
    public async Task MultipleCapturesAllSubstituted()
    {
        var alias = new AliasRule { Pattern = @"^cast (\w+) (\w+)$", Expansion = "cast spell $1 on $2" };

        var result = _engine.Expand("cast fireball dragon", [alias]);

        await Assert.That(result).IsEqualTo("cast spell fireball on dragon");
    }

    [Test]
    public async Task DisabledFirstEnabledSecondSecondApplied()
    {
        var disabled = new AliasRule { Pattern = @"^k (.+)$", Expansion = "kill $1", Enabled = false };
        var enabled = new AliasRule { Pattern = @"^k (.+)$", Expansion = "slay $1" };

        var result = _engine.Expand("k orc", [disabled, enabled]);

        await Assert.That(result).IsEqualTo("slay orc");
    }

    [Test]
    public async Task UnmatchedGroupRefBecomesEmptyString()
    {
        // Pattern has one capture group, expansion references $2 which doesn't exist
        var alias = new AliasRule { Pattern = @"^go (\w+)$", Expansion = "move $1 $2" };

        var result = _engine.Expand("go north", [alias]);

        await Assert.That(result).IsEqualTo("move north ");
    }
}
