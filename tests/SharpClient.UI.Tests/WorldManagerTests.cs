using Bunit;
using SharpClient.Core.Domain;
using SharpClient.Core.Presentation;
using SharpClient.Core.Sessions;
using SharpClient.UI.Components;

namespace SharpClient.UI.Tests;

public sealed class WorldManagerTests
{
    private static async Task<WorldManagerViewModel> BuildSeededVmAsync()
    {
        var store = new UiFakeWorldStore();
        var world = new World { Name = "Sindome", Host = "sindome.org", Port = 5555 };
        world.Characters.Add(new Character { WorldId = world.Id, Name = "Vesper" });
        await store.AddWorldAsync(world);

        var vm = new WorldManagerViewModel(store, new UiFakeSecretStore(), new SessionManager(), new UiFakeSessionLauncher());
        await vm.LoadAsync();
        return vm;
    }

    [Test]
    public async Task RendersWorldName()
    {
        var vm = await BuildSeededVmAsync();
        using var ctx = new BunitContext();

        var cut = ctx.Render<WorldManager>(p => p.Add(c => c.Vm, vm));

        await Assert.That(cut.Markup).Contains("Sindome");
    }

    [Test]
    public async Task ExpandingWorldShowsClickableCharacterLabel()
    {
        var vm = await BuildSeededVmAsync();
        using var ctx = new BunitContext();
        var cut = ctx.Render<WorldManager>(p => p.Add(c => c.Vm, vm));

        cut.Find(".sc-world-row").Click();

        await Assert.That(cut.Markup).Contains("Vesper");
        // Connecting is now driven by clicking the character's label area, not a separate button.
        await Assert.That(cut.FindAll(".sc-char-main")).IsNotEmpty();
    }

    [Test]
    public async Task FailedConnectRendersDismissibleErrorBanner()
    {
        var store = new UiFakeWorldStore();
        var world = new World { Name = "Sindome", Host = "sindome.org", Port = 5555 };
        world.Characters.Add(new Character { WorldId = world.Id, Name = "Vesper" });
        await store.AddWorldAsync(world);

        var launcher = new UiFakeSessionLauncher { ThrowOnLaunch = new InvalidOperationException("connection refused") };
        var vm = new WorldManagerViewModel(store, new UiFakeSecretStore(), new SessionManager(), launcher);
        await vm.LoadAsync();

        using var ctx = new BunitContext();
        var cut = ctx.Render<WorldManager>(p => p.Add(c => c.Vm, vm));

        cut.Find(".sc-world-row").Click();
        cut.Find(".sc-char-main").Click();

        // The failure surfaces as a banner instead of throwing.
        await Assert.That(cut.FindAll(".sc-wm-error")).IsNotEmpty();
        await Assert.That(cut.Markup).Contains("connection refused");

        // Dismissing it removes the banner.
        cut.Find(".sc-wm-error-dismiss").Click();
        await Assert.That(cut.FindAll(".sc-wm-error")).IsEmpty();
    }

    [Test]
    public async Task EmptyStateRendersWhenNoWorlds()
    {
        var vm = new WorldManagerViewModel(new UiFakeWorldStore(), new UiFakeSecretStore(), new SessionManager(), new UiFakeSessionLauncher());
        await vm.LoadAsync();
        using var ctx = new BunitContext();

        var cut = ctx.Render<WorldManager>(p => p.Add(c => c.Vm, vm));

        await Assert.That(cut.Markup).Contains("No worlds yet");
        await Assert.That(cut.FindAll(".sc-wm-empty-cta")).IsNotEmpty();
    }
}
