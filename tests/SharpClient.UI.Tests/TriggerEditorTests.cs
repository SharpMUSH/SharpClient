using Bunit;
using SharpClient.Core.Domain;
using SharpClient.Core.Presentation;
using SharpClient.UI.Components;

namespace SharpClient.UI.Tests;

public sealed class TriggerEditorTests
{
    private static async Task<(TriggerAliasEditorViewModel vm, World world, UiFakeWorldStore store)> BuildSeededVmAsync()
    {
        var store = new UiFakeWorldStore();
        var world = new World { Name = "TestMud" };
        world.Triggers.Add(new TriggerRule
        {
            Pattern = "hp: [0-9]+",
            Kind = TriggerKind.Regex,
            Action = TriggerActionKind.Highlight,
            ActionValue = "2",
            Enabled = true,
        });
        world.Aliases.Add(new AliasRule
        {
            Pattern = "^k (.+)$",
            Expansion = "kill $1",
            Enabled = true,
        });
        await store.AddWorldAsync(world);

        var vm = new TriggerAliasEditorViewModel(store);
        await vm.LoadAsync(world.Id);
        return (vm, world, store);
    }

    [Test]
    public async Task RendersTriggerPatternText()
    {
        var (vm, _, _) = await BuildSeededVmAsync();
        using var ctx = new BunitContext();

        var cut = ctx.Render<TriggerEditor>(p => p.Add(c => c.Vm, vm));

        await Assert.That(cut.Markup).Contains("hp: [0-9]+");
    }

    [Test]
    public async Task RendersAliasPatternText()
    {
        var (vm, _, _) = await BuildSeededVmAsync();
        using var ctx = new BunitContext();

        var cut = ctx.Render<TriggerEditor>(p => p.Add(c => c.Vm, vm));

        await Assert.That(cut.Markup).Contains("^k (.+)$");
    }

    [Test]
    public async Task KindBadgeShowsRegexForRegexTrigger()
    {
        var (vm, _, _) = await BuildSeededVmAsync();
        using var ctx = new BunitContext();

        var cut = ctx.Render<TriggerEditor>(p => p.Add(c => c.Vm, vm));

        var badges = cut.FindAll(".sc-rule-kind");
        // badges: [regex trigger badge, alias badge]
        await Assert.That(badges[0].TextContent.Trim()).IsEqualTo("regex");
    }

    [Test]
    public async Task KindBadgeShowsAliasForAlias()
    {
        var (vm, _, _) = await BuildSeededVmAsync();
        using var ctx = new BunitContext();

        var cut = ctx.Render<TriggerEditor>(p => p.Add(c => c.Vm, vm));

        var badges = cut.FindAll(".sc-rule-kind");
        // second badge is the alias row
        await Assert.That(badges[1].TextContent.Trim()).IsEqualTo("alias");
    }

    [Test]
    public async Task TriggerRowsRenderWithCorrectCount()
    {
        var (vm, _, _) = await BuildSeededVmAsync();
        using var ctx = new BunitContext();

        var cut = ctx.Render<TriggerEditor>(p => p.Add(c => c.Vm, vm));

        // 2 rule rows total (1 trigger + 1 alias)
        await Assert.That(cut.FindAll(".sc-rule")).Count().IsEqualTo(2);
    }

    [Test]
    public async Task ToggleTriggerFlipsEnabledOnVm()
    {
        var (vm, _, _) = await BuildSeededVmAsync();
        using var ctx = new BunitContext();
        var cut = ctx.Render<TriggerEditor>(p => p.Add(c => c.Vm, vm));

        // Find the first toggle (the trigger's checkbox)
        var toggles = cut.FindAll(".sc-rule-toggle");
        await cut.InvokeAsync(async () =>
            await toggles[0].TriggerEventAsync("onchange",
                new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = false }));

        await Assert.That(vm.Triggers[0].Enabled).IsFalse();
    }

    [Test]
    public async Task ToggleAliasFlipsEnabledOnVm()
    {
        var (vm, _, _) = await BuildSeededVmAsync();
        using var ctx = new BunitContext();
        var cut = ctx.Render<TriggerEditor>(p => p.Add(c => c.Vm, vm));

        // second toggle is the alias
        var toggles = cut.FindAll(".sc-rule-toggle");
        await cut.InvokeAsync(async () =>
            await toggles[1].TriggerEventAsync("onchange",
                new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = false }));

        await Assert.That(vm.Aliases[0].Enabled).IsFalse();
    }

    [Test]
    public async Task EmptyStateRendersWhenNoTriggersOrAliases()
    {
        var store = new UiFakeWorldStore();
        var world = new World { Name = "Empty" };
        await store.AddWorldAsync(world);
        var vm = new TriggerAliasEditorViewModel(store);
        await vm.LoadAsync(world.Id);
        using var ctx = new BunitContext();

        var cut = ctx.Render<TriggerEditor>(p => p.Add(c => c.Vm, vm));

        await Assert.That(cut.Markup).Contains("no triggers yet");
        await Assert.That(cut.Markup).Contains("no aliases yet");
    }

    [Test]
    public async Task TwoSectionsRenderTriggersAndAliases()
    {
        var (vm, _, _) = await BuildSeededVmAsync();
        using var ctx = new BunitContext();

        var cut = ctx.Render<TriggerEditor>(p => p.Add(c => c.Vm, vm));

        var sections = cut.FindAll(".sc-rule-section");
        await Assert.That(sections).Count().IsEqualTo(2);
        await Assert.That(cut.Markup).Contains("Triggers");
        await Assert.That(cut.Markup).Contains("Aliases");
    }

    // ── Inline edit tests ──────────────────────────────────────────────────

    [Test]
    public async Task EditButtonIsRenderedPerTriggerRow()
    {
        var (vm, _, _) = await BuildSeededVmAsync();
        using var ctx = new BunitContext();

        var cut = ctx.Render<TriggerEditor>(p => p.Add(c => c.Vm, vm));

        var editBtns = cut.FindAll(".sc-rule-edit-btn");
        await Assert.That(editBtns).Count().IsGreaterThanOrEqualTo(1);
    }

    [Test]
    public async Task ClickingEditTriggerButtonOpensInlineForm()
    {
        var (vm, _, _) = await BuildSeededVmAsync();
        using var ctx = new BunitContext();
        var cut = ctx.Render<TriggerEditor>(p => p.Add(c => c.Vm, vm));

        var editBtns = cut.FindAll(".sc-rule-edit-btn");
        await cut.InvokeAsync(() => editBtns[0].Click());

        await Assert.That(cut.Markup).Contains("sc-rule-inline-form--trigger");
    }

    [Test]
    public async Task InlineTriggerFormIsPrefilledWithRuleValues()
    {
        var (vm, _, _) = await BuildSeededVmAsync();
        using var ctx = new BunitContext();
        var cut = ctx.Render<TriggerEditor>(p => p.Add(c => c.Vm, vm));

        var editBtns = cut.FindAll(".sc-rule-edit-btn");
        await cut.InvokeAsync(() => editBtns[0].Click());

        // The inline form should contain the existing pattern value
        await Assert.That(cut.Markup).Contains("hp: [0-9]+");
    }

    [Test]
    public async Task SaveEditTriggerUpdatesVm()
    {
        var (vm, _, _) = await BuildSeededVmAsync();
        using var ctx = new BunitContext();
        var cut = ctx.Render<TriggerEditor>(p => p.Add(c => c.Vm, vm));

        // Open the edit form
        var editBtns = cut.FindAll(".sc-rule-edit-btn");
        await cut.InvokeAsync(() => editBtns[0].Click());

        // Change pattern value via the inline inputs
        var form = cut.Find(".sc-rule-inline-form--trigger");
        var inputs = form.QuerySelectorAll("input[type=text], input:not([type])");
        // inputs[0] = pattern, inputs[1] = value
        await cut.InvokeAsync(() =>
            inputs[0].Input(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = "new-pattern" }));

        // Click Save
        var saveBtn = form.QuerySelector("button.sc-rule-add-btn")!;
        await cut.InvokeAsync(async () => await saveBtn.ClickAsync(new()));

        await Assert.That(vm.Triggers[0].Pattern).IsEqualTo("new-pattern");
    }

    [Test]
    public async Task CancelEditTriggerClosesForm()
    {
        var (vm, _, _) = await BuildSeededVmAsync();
        using var ctx = new BunitContext();
        var cut = ctx.Render<TriggerEditor>(p => p.Add(c => c.Vm, vm));

        var editBtns = cut.FindAll(".sc-rule-edit-btn");
        await cut.InvokeAsync(() => editBtns[0].Click());

        // Click Cancel (second button in the inline form)
        var form = cut.Find(".sc-rule-inline-form--trigger");
        var cancelBtn = form.QuerySelectorAll("button")[1];
        await cut.InvokeAsync(() => cancelBtn.Click());

        await Assert.That(cut.Markup).DoesNotContain("sc-rule-inline-form--trigger");
    }

    [Test]
    public async Task EditButtonIsRenderedPerAliasRow()
    {
        var (vm, _, _) = await BuildSeededVmAsync();
        using var ctx = new BunitContext();

        var cut = ctx.Render<TriggerEditor>(p => p.Add(c => c.Vm, vm));

        var editBtns = cut.FindAll(".sc-rule-edit-btn");
        // 1 trigger row + 1 alias row → 2 edit buttons
        await Assert.That(editBtns).Count().IsEqualTo(2);
    }

    [Test]
    public async Task ClickingEditAliasButtonOpensInlineForm()
    {
        var (vm, _, _) = await BuildSeededVmAsync();
        using var ctx = new BunitContext();
        var cut = ctx.Render<TriggerEditor>(p => p.Add(c => c.Vm, vm));

        var editBtns = cut.FindAll(".sc-rule-edit-btn");
        // second edit btn belongs to the alias
        await cut.InvokeAsync(() => editBtns[1].Click());

        await Assert.That(cut.Markup).Contains("sc-rule-inline-form--alias");
    }

    [Test]
    public async Task ScopeSelectorIsRendered()
    {
        var (vm, _, _) = await BuildSeededVmAsync();
        using var ctx = new BunitContext();

        var cut = ctx.Render<TriggerEditor>(p => p.Add(c => c.Vm, vm));

        await Assert.That(cut.Markup).Contains("sc-rule-scope-select");
        await Assert.That(cut.Markup).Contains("World");
    }

    [Test]
    public async Task ScopeSelectorShowsCharactersFromVm()
    {
        var store = new UiFakeWorldStore();
        var world = new World { Name = "TestMud" };
        var ch = new SharpClient.Core.Domain.Character { Name = "Mannaz", WorldId = world.Id };
        world.Characters.Add(ch);
        world.Triggers.Add(new TriggerRule { Pattern = "x", Kind = TriggerKind.Substring, Action = TriggerActionKind.Send, ActionValue = "y" });
        await store.AddWorldAsync(world);
        var vm = new TriggerAliasEditorViewModel(store);
        await vm.LoadAsync(world.Id);
        using var ctx = new BunitContext();

        var cut = ctx.Render<TriggerEditor>(p => p.Add(c => c.Vm, vm));

        await Assert.That(cut.Markup).Contains("Mannaz");
    }

    [Test]
    public async Task CharacterScopeRulesAreSeparateFromWorldRules()
    {
        var store = new UiFakeWorldStore();
        var world = new World { Name = "TestMud" };
        var ch = new SharpClient.Core.Domain.Character { Name = "Mannaz", WorldId = world.Id };
        world.Triggers.Add(new TriggerRule { Pattern = "world-trigger", Kind = TriggerKind.Substring, Action = TriggerActionKind.Send, ActionValue = "w" });
        ch.Triggers.Add(new TriggerRule { Pattern = "char-trigger", Kind = TriggerKind.Substring, Action = TriggerActionKind.Send, ActionValue = "c" });
        world.Characters.Add(ch);
        await store.AddWorldAsync(world);
        var vm = new TriggerAliasEditorViewModel(store);
        await vm.LoadAsync(world.Id);

        // Switch to character scope
        vm.SetScope(ch.Id);

        using var ctx = new BunitContext();
        var cut = ctx.Render<TriggerEditor>(p => p.Add(c => c.Vm, vm));

        await Assert.That(cut.Markup).Contains("char-trigger");
        await Assert.That(cut.Markup).DoesNotContain("world-trigger");
    }
}
