using Bunit;
using Microsoft.AspNetCore.Components.Web;
using SharpClient.Core.Connection;
using SharpClient.Core.Formatting;
using SharpClient.Core.Presentation;
using SharpClient.Core.Sessions;
using SharpClient.UI.Components;

namespace SharpClient.UI.Tests;

file sealed class ComposeFakePrefs : SharpClient.Core.Platform.IPreferences
{
    private readonly Dictionary<string, string> _store = [];
    public string GetString(string key, string def) => _store.TryGetValue(key, out var v) ? v : def;
    public void SetString(string key, string value) => _store[key] = value;
    public int GetInt(string key, int def) => _store.TryGetValue(key, out var v) && int.TryParse(v, out var i) ? i : def;
    public void SetInt(string key, int value) => _store[key] = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    public bool GetBool(string key, bool def) => _store.TryGetValue(key, out var v) && bool.TryParse(v, out var b) ? b : def;
    public void SetBool(string key, bool value) => _store[key] = value.ToString();
}

public sealed class ComposeViewTests
{
    private static (ComposeViewModel vm, UiFakeSession session) Connected()
    {
        var mgr = new SessionManager();
        var session = new UiFakeSession { State = ConnectionState.Connected };
        mgr.Add(session);
        return (new ComposeViewModel(mgr, new ComposeFakePrefs()), session);
    }

    [Test]
    public async Task SendIsDisabledWhenBodyIsEmpty()
    {
        using var ctx = new BunitContext();
        var (vm, _) = Connected();

        var cut = ctx.Render<ComposeView>(p => p.Add(c => c.Vm, vm));

        await Assert.That(cut.Find(".sc-send-btn").HasAttribute("disabled")).IsTrue();
    }

    [Test]
    public async Task SendIsEnabledWhenBodyIsSet()
    {
        using var ctx = new BunitContext();
        var (vm, _) = Connected();
        vm.Body = "waves";

        var cut = ctx.Render<ComposeView>(p => p.Add(c => c.Vm, vm));

        await Assert.That(cut.Find(".sc-send-btn").HasAttribute("disabled")).IsFalse();
    }

    [Test]
    public async Task ClickingSendDeliversTheFormattedLine()
    {
        using var ctx = new BunitContext();
        var (vm, session) = Connected();
        vm.Body = "grins\n100% sure";

        var cut = ctx.Render<ComposeView>(p => p.Add(c => c.Vm, vm));
        await cut.Find(".sc-send-btn").ClickAsync(new MouseEventArgs());

        await Assert.That(session.Sent).Contains("pose grins%r100%% sure");
        await Assert.That(vm.Body).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task CtrlEnterInTheEditorSendsAndClearsTheDraft()
    {
        using var ctx = new BunitContext();
        var (vm, session) = Connected();
        vm.Body = "grins";

        var cut = ctx.Render<ComposeView>(p => p.Add(c => c.Vm, vm));
        await cut.Find(".sc-compose-input").KeyDownAsync(new KeyboardEventArgs { Key = "Enter", CtrlKey = true });

        await Assert.That(session.Sent).Contains("pose grins");
        await Assert.That(vm.Body).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task BareEnterInTheEditorDoesNotSend()
    {
        using var ctx = new BunitContext();
        var (vm, session) = Connected();
        vm.Body = "grins";

        var cut = ctx.Render<ComposeView>(p => p.Add(c => c.Vm, vm));
        await cut.Find(".sc-compose-input").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });

        await Assert.That(session.Sent).IsEmpty();
        await Assert.That(vm.Body).IsEqualTo("grins");
    }

    [Test]
    public async Task PreviewToggleShowsTheWireTextThenReturnsToTheEditor()
    {
        using var ctx = new BunitContext();
        var (vm, _) = Connected();
        vm.Body = "grins";

        var cut = ctx.Render<ComposeView>(p => p.Add(c => c.Vm, vm));
        await cut.Find(".sc-compose-toggle").ClickAsync(new MouseEventArgs());

        await Assert.That(cut.Find(".sc-compose-preview").TextContent).IsEqualTo("pose grins");
        await Assert.That(cut.FindAll(".sc-compose-input")).IsEmpty();

        await cut.Find(".sc-compose-toggle").ClickAsync(new MouseEventArgs());

        await Assert.That(cut.FindAll(".sc-compose-input")).Count().IsEqualTo(1);
        await Assert.That(vm.Body).IsEqualTo("grins");
    }

    [Test]
    public async Task SelectingAChipChangesThePreviewedCommand()
    {
        using var ctx = new BunitContext();
        var (vm, _) = Connected();
        vm.Body = "waves";

        var cut = ctx.Render<ComposeView>(p => p.Add(c => c.Vm, vm));
        var sayChip = cut.FindAll(".sc-compose-chip")[0];
        await sayChip.ClickAsync(new MouseEventArgs());

        await Assert.That(vm.SelectedPrefix).IsEqualTo(PosePrefix.Say);
        await cut.Find(".sc-compose-toggle").ClickAsync(new MouseEventArgs());
        await Assert.That(cut.Find(".sc-compose-preview").TextContent).IsEqualTo("say waves");
    }

    [Test]
    public async Task CustomChipRevealsThePrefixField()
    {
        using var ctx = new BunitContext();
        var (vm, _) = Connected();

        var cut = ctx.Render<ComposeView>(p => p.Add(c => c.Vm, vm));
        await Assert.That(cut.FindAll(".sc-compose-custom")).IsEmpty();

        var customChip = cut.FindAll(".sc-compose-chip")[4];
        await customChip.ClickAsync(new MouseEventArgs());

        await Assert.That(cut.FindAll(".sc-compose-custom")).Count().IsEqualTo(1);
    }

    [Test]
    public async Task HintIsShownWhenNoSessionIsConnected()
    {
        using var ctx = new BunitContext();
        var mgr = new SessionManager();
        mgr.Add(new UiFakeSession { State = ConnectionState.Disconnected });
        var vm = new ComposeViewModel(mgr, new ComposeFakePrefs());

        var cut = ctx.Render<ComposeView>(p => p.Add(c => c.Vm, vm));

        await Assert.That(cut.FindAll(".sc-compose-hint")).Count().IsEqualTo(1);
    }

    [Test]
    public async Task ClearEmptiesTheEditor()
    {
        using var ctx = new BunitContext();
        var (vm, _) = Connected();
        vm.Body = "waves";

        var cut = ctx.Render<ComposeView>(p => p.Add(c => c.Vm, vm));
        await cut.Find(".sc-compose-clear").ClickAsync(new MouseEventArgs());

        await Assert.That(vm.Body).IsEqualTo(string.Empty);
    }
}
