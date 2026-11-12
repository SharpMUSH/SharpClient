using Bunit;
using SharpClient.Core.Rendering;
using SharpClient.Core.Sessions;
using SharpClient.UI.Components;

namespace SharpClient.UI.Tests;

public sealed class OutputViewTests
{
    private static ScrollbackLine[] Lines(string raw) =>
        [.. new OutputDecoder().Decode(raw).Select(content => new ScrollbackLine(content))];

    private static BunitContext NewContext()
    {
        var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        return ctx;
    }

    [Test]
    public async Task EachLineIsMarkupStringHtml()
    {
        using var ctx = NewContext();
        var lines = Lines("\e[31mred\e[0m plain");

        var cut = ctx.Render<OutputView>(p => p.Add(c => c.Lines, lines));

        var line = cut.Find("div.sc-line");
        await Assert.That(line.InnerHtml).IsEqualTo(OutputHtml.Render(lines[0].Content));
        await Assert.That(line.QuerySelector("span")!.GetAttribute("style")).StartsWith("color: var(--ms-ansi-1,");
        await Assert.That(line.TextContent).IsEqualTo("red plain");
    }

    [Test]
    public async Task TheOutputCarriesTheClientPalette()
    {
        using var ctx = NewContext();

        var cut = ctx.Render<OutputView>(p => p.Add(c => c.Lines, Lines("x")));

        await Assert.That(cut.Find("div.sc-output").GetAttribute("style")).IsEqualTo(OutputPalette.CustomProperties);
    }

    [Test]
    public async Task RepeatedLinesAreEachShown()
    {
        using var ctx = NewContext();
        var first = Lines("You say, \"hi\"");
        var cut = ctx.Render<OutputView>(p => p.Add(c => c.Lines, first));

        // Blazor matches lines by key when it re-renders, so two equal keys fail there.
        ScrollbackLine[] both = [.. first, .. Lines("You say, \"hi\"")];
        cut.Render(p => p.Add(c => c.Lines, both));

        await Assert.That(cut.FindAll("div.sc-line").Count).IsEqualTo(2);
    }

    [Test]
    public async Task EmptyLineRendersAnEmptyLine()
    {
        using var ctx = NewContext();

        var cut = ctx.Render<OutputView>(p => p.Add(c => c.Lines, Lines(string.Empty)));

        await Assert.That(cut.FindAll("div.sc-line").Count).IsEqualTo(1);
        await Assert.That(cut.Find("div.sc-line").InnerHtml).IsEmpty();
    }

    [Test]
    public async Task AttachesMarkupStringWebWithoutSoundAndKeepsItsLines()
    {
        using var ctx = new BunitContext();
        var module = ctx.JSInterop.SetupModule(OutputView.ScriptPath);
        module.SetupModule("attachDotNet", _ => true);

        ctx.Render<OutputView>(p => p.Add(c => c.Lines, Lines("x")));

        var call = module.VerifyInvoke("attachDotNet");
        var options = call.Arguments[2]!;
        var forward = (string[])options.GetType().GetProperty("forward")!.GetValue(options)!;
        await Assert.That(options.GetType().GetProperty("sounds")!.GetValue(options) as bool?).IsFalse();
        // The lines are Blazor's: the script must not clear them or move their text to a pane.
        await Assert.That(forward).Contains("clear");
        await Assert.That(forward).Contains("pane");
    }

    [Test]
    public async Task DisposingDetachesTheScript()
    {
        using var ctx = new BunitContext();
        var module = ctx.JSInterop.SetupModule(OutputView.ScriptPath);
        var runtime = module.SetupModule("attachDotNet", _ => true);
        runtime.SetupVoid("detach").SetVoidResult();
        var cut = ctx.Render<OutputView>(p => p.Add(c => c.Lines, Lines("x")));

        await cut.Instance.DisposeAsync();

        runtime.VerifyInvoke("detach");
    }

    [Test]
    public async Task CommandsFromTheScriptAreSent()
    {
        using var ctx = NewContext();
        var sent = new List<string>();
        var cut = ctx.Render<OutputView>(p => p
            .Add(c => c.Lines, Lines("x"))
            .Add(c => c.OnSendCommand, cmd => { sent.Add(cmd); return Task.CompletedTask; }));

        await cut.Instance.Send("go north");
        await cut.Instance.Prompt("say ");

        await Assert.That(sent).IsEquivalentTo(["go north", "say "]);
    }

    [Test]
    public async Task CommandWithoutAHandlerDoesNothing()
    {
        using var ctx = NewContext();
        var cut = ctx.Render<OutputView>(p => p.Add(c => c.Lines, Lines("x")));

        await cut.Instance.Send("go north");
    }

    [Test]
    public async Task TriggerHighlightCoversTheLine()
    {
        using var ctx = NewContext();
        var rule = new SharpClient.Core.Domain.TriggerRule
        {
            Kind = SharpClient.Core.Domain.TriggerKind.Substring,
            Pattern = "danger",
            Action = SharpClient.Core.Domain.TriggerActionKind.Highlight,
            ActionValue = "9",
        };
        const string raw = "\e[32mdanger\e[0m ahead";
        var outcome = new SharpClient.Core.Triggers.TriggerEngine().Apply(raw, new OutputDecoder().Decode(raw), [rule]);

        var cut = ctx.Render<OutputView>(p => p.Add(c => c.Lines, outcome.Lines.Select(l => new ScrollbackLine(l)).ToArray()));

        var highlight = cut.Find("div.sc-line > span.sc-highlight");
        await Assert.That(highlight.GetAttribute("style")).IsEqualTo("--sc-highlight: #ff8088");
        await Assert.That(highlight.TextContent).IsEqualTo("danger ahead");
    }
}
