using Bunit;
using Microsoft.Extensions.DependencyInjection;
using SharpClient.Core.Diagnostics;
using SharpClient.Core.Presentation;
using SharpClient.UI.Components;

namespace SharpClient.UI.Tests;

// Minimal FakePreferences local to UI tests — avoids cross-project dependency on SharpClient.Tests
file sealed class LocalFakePrefs : SharpClient.Core.Platform.IPreferences
{
    private readonly Dictionary<string, string> _store = [];
    public string GetString(string key, string def) => _store.TryGetValue(key, out var v) ? v : def;
    public void SetString(string key, string value) => _store[key] = value;
    public int GetInt(string key, int def) => _store.TryGetValue(key, out var v) && int.TryParse(v, out var i) ? i : def;
    public void SetInt(string key, int value) => _store[key] = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    public bool GetBool(string key, bool def) => _store.TryGetValue(key, out var v) && bool.TryParse(v, out var b) ? b : def;
    public void SetBool(string key, bool value) => _store[key] = value.ToString();
}

public sealed class SettingsViewTests
{
    private static SettingsViewModel MakeVm() => new(new LocalFakePrefs());

    // SettingsView injects ILogExporter; register a no-op so renders resolve. IsAvailable == false
    // hides the Diagnostics section, leaving the assertions below (font/accent/slider counts) intact.
    private static BunitContext NewContext()
    {
        var ctx = new BunitContext();
        ctx.Services.AddSingleton<ILogExporter>(new NoopLogExporter());
        return ctx;
    }

    [Test]
    public async Task RendersAllFourFontOptions()
    {
        using var ctx = NewContext();
        var vm = MakeVm();

        var cut = ctx.Render<SettingsView>(p => p.Add(c => c.Vm, vm));

        var opts = cut.FindAll(".sc-font-option");
        await Assert.That(opts.Count).IsEqualTo(4);
    }

    [Test]
    public async Task RendersAllFiveAccentSwatches()
    {
        using var ctx = NewContext();
        var vm = MakeVm();

        var cut = ctx.Render<SettingsView>(p => p.Add(c => c.Vm, vm));

        var swatches = cut.FindAll(".sc-accent-swatch");
        await Assert.That(swatches.Count).IsEqualTo(5);
    }

    [Test]
    public async Task RendersGlowAndScanlineToggles()
    {
        using var ctx = NewContext();
        var vm = MakeVm();

        var cut = ctx.Render<SettingsView>(p => p.Add(c => c.Vm, vm));

        // Two checkboxes — one for glow, one for scanlines
        var checks = cut.FindAll("input[type='checkbox']");
        await Assert.That(checks.Count).IsGreaterThanOrEqualTo(2);
    }

    [Test]
    public async Task ClickingAccentSwatchUpdatesVmAccent()
    {
        using var ctx = NewContext();
        var vm = MakeVm();
        vm.Accent = "#9b7ed4"; // start with default

        var cut = ctx.Render<SettingsView>(p => p.Add(c => c.Vm, vm));

        // Click the second swatch (#39d98a)
        var swatches = cut.FindAll(".sc-accent-swatch");
        swatches[1].Click();

        await Assert.That(vm.Accent).IsEqualTo("#39d98a");
    }

    [Test]
    public async Task ClickingFontOptionUpdatesVmFont()
    {
        using var ctx = NewContext();
        var vm = MakeVm();

        var cut = ctx.Render<SettingsView>(p => p.Add(c => c.Vm, vm));

        // Click the second font option (IBM Plex Mono)
        var opts = cut.FindAll(".sc-font-option");
        opts[1].Click();

        await Assert.That(vm.OutputFont).IsEqualTo("IBM Plex Mono");
    }

    [Test]
    public async Task MinColumnsStepperRendersWithCorrectValue()
    {
        using var ctx = NewContext();
        var vm = MakeVm();
        vm.MinColumns = 90;

        var cut = ctx.Render<SettingsView>(p => p.Add(c => c.Vm, vm));

        // Min columns is now a typeable number stepper (not a slider).
        var input = cut.Find("input.sc-stepper-input");
        await Assert.That(input.GetAttribute("value")).IsEqualTo("90");
    }

    [Test]
    public async Task MinColumnsStepperButtonsAdjustValue()
    {
        using var ctx = NewContext();
        var vm = MakeVm();
        vm.MinColumns = 90;

        var cut = ctx.Render<SettingsView>(p => p.Add(c => c.Vm, vm));

        var buttons = cut.FindAll(".sc-stepper-btn");
        buttons[1].Click(); // "+"
        await Assert.That(vm.MinColumns).IsEqualTo(91);
        buttons[0].Click(); // "−"
        buttons[0].Click();
        await Assert.That(vm.MinColumns).IsEqualTo(89);
    }

    [Test]
    public async Task MaxFontSizeSliderRendersWithCorrectValue()
    {
        using var ctx = NewContext();
        var vm = MakeVm();
        vm.MaxFontSize = 16;

        var cut = ctx.Render<SettingsView>(p => p.Add(c => c.Vm, vm));

        var slider = cut.Find("input[type='range'][min='6']");
        await Assert.That(slider.GetAttribute("value")).IsEqualTo("16");
    }
}
