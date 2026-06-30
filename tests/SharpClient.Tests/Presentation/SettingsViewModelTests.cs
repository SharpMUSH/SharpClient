using SharpClient.Core.Presentation;
using SharpClient.Tests.Fakes;

namespace SharpClient.Tests.Presentation;

public sealed class SettingsViewModelTests
{
    private static (SettingsViewModel vm, FakePreferences prefs) Build()
    {
        var prefs = new FakePreferences();
        var vm = new SettingsViewModel(prefs);
        return (vm, prefs);
    }

    [Test]
    public async Task DefaultsLoadCorrectly()
    {
        var (vm, _) = Build();
        await Assert.That(vm.OutputFont).IsEqualTo("JetBrains Mono");
        await Assert.That(vm.MinColumns).IsEqualTo(78);
        await Assert.That(vm.MaxFontSize).IsEqualTo(14);
        await Assert.That(vm.Accent).IsEqualTo("#9b7ed4");
        await Assert.That(vm.Glow).IsTrue();
        await Assert.That(vm.Scanlines).IsTrue();
    }

    [Test]
    public async Task SettingOutputFontPersistsAndFiresChanged()
    {
        var (vm, prefs) = Build();
        var fired = 0;
        vm.Changed += () => fired++;

        vm.OutputFont = "IBM Plex Mono";

        await Assert.That(prefs.GetString("OutputFont", "")).IsEqualTo("IBM Plex Mono");
        await Assert.That(fired).IsEqualTo(1);
    }

    [Test]
    public async Task SettingMinColumnsPersistsAndFiresChanged()
    {
        var (vm, prefs) = Build();
        var fired = 0;
        vm.Changed += () => fired++;

        vm.MinColumns = 100;

        await Assert.That(prefs.GetInt("MinColumns", 0)).IsEqualTo(100);
        await Assert.That(fired).IsEqualTo(1);
    }

    [Test]
    public async Task SettingMaxFontSizePersistsAndFiresChanged()
    {
        var (vm, prefs) = Build();
        var fired = 0;
        vm.Changed += () => fired++;

        vm.MaxFontSize = 16;

        await Assert.That(prefs.GetInt("MaxFontSize", 0)).IsEqualTo(16);
        await Assert.That(fired).IsEqualTo(1);
    }

    [Test]
    public async Task SettingAccentPersistsAndFiresChanged()
    {
        var (vm, prefs) = Build();
        var fired = 0;
        vm.Changed += () => fired++;

        vm.Accent = "#39d98a";

        await Assert.That(prefs.GetString("Accent", "")).IsEqualTo("#39d98a");
        await Assert.That(fired).IsEqualTo(1);
    }

    [Test]
    public async Task SettingGlowPersistsAndFiresChanged()
    {
        var (vm, prefs) = Build();
        var fired = 0;
        vm.Changed += () => fired++;

        vm.Glow = false;

        await Assert.That(prefs.GetBool("Glow", true)).IsFalse();
        await Assert.That(fired).IsEqualTo(1);
    }

    [Test]
    public async Task SettingScanlinesFiresChanged()
    {
        var (vm, prefs) = Build();
        var fired = 0;
        vm.Changed += () => fired++;

        vm.Scanlines = false;

        await Assert.That(prefs.GetBool("Scanlines", true)).IsFalse();
        await Assert.That(fired).IsEqualTo(1);
    }

    [Test]
    public async Task ReconstructedVmReadsBackSavedValues()
    {
        var prefs = new FakePreferences();
        var vm1 = new SettingsViewModel(prefs);
        vm1.OutputFont = "Space Mono";
        vm1.MinColumns = 90;
        vm1.MaxFontSize = 16;
        vm1.Accent = "#4a90d9";
        vm1.Glow = false;
        vm1.Scanlines = false;

        var vm2 = new SettingsViewModel(prefs);
        await Assert.That(vm2.OutputFont).IsEqualTo("Space Mono");
        await Assert.That(vm2.MinColumns).IsEqualTo(90);
        await Assert.That(vm2.MaxFontSize).IsEqualTo(16);
        await Assert.That(vm2.Accent).IsEqualTo("#4a90d9");
        await Assert.That(vm2.Glow).IsFalse();
        await Assert.That(vm2.Scanlines).IsFalse();
    }

    [Test]
    public async Task RootStyleVariablesContainsAccentAndFontAndSize()
    {
        var (vm, _) = Build();
        vm.Accent = "#39d98a";
        vm.OutputFont = "IBM Plex Mono";
        vm.MaxFontSize = 12;

        var css = vm.RootStyleVariables;

        await Assert.That(css).Contains("#39d98a");
        await Assert.That(css).Contains("IBM Plex Mono");
        await Assert.That(css).Contains("12px");
    }

    [Test]
    public async Task FontFamilyMapsJetBrainsMono()
    {
        var (vm, _) = Build();
        vm.OutputFont = "JetBrains Mono";
        await Assert.That(vm.FontFamily).Contains("JetBrains Mono");
    }

    [Test]
    public async Task FontFamilyMapsIbmPlexMono()
    {
        var (vm, _) = Build();
        vm.OutputFont = "IBM Plex Mono";
        await Assert.That(vm.FontFamily).Contains("IBM Plex Mono");
    }

    [Test]
    public async Task FontFamilyMapsSpaceMono()
    {
        var (vm, _) = Build();
        vm.OutputFont = "Space Mono";
        await Assert.That(vm.FontFamily).Contains("Space Mono");
    }

    [Test]
    public async Task FontFamilyMapsCourier()
    {
        var (vm, _) = Build();
        vm.OutputFont = "Courier";
        await Assert.That(vm.FontFamily).Contains("Courier");
    }

    [Test]
    public async Task FontOptionsContainsFourEntries()
    {
        await Assert.That(SettingsViewModel.FontOptions).Count().IsEqualTo(4);
    }

    [Test]
    public async Task AccentOptionsContainsFiveEntries()
    {
        await Assert.That(SettingsViewModel.AccentOptions).Count().IsEqualTo(5);
    }
}
