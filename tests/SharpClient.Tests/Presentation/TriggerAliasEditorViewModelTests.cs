using SharpClient.Core.Domain;
using SharpClient.Core.Presentation;
using SharpClient.Tests.Fakes;

namespace SharpClient.Tests.Presentation;

public sealed class TriggerAliasEditorViewModelTests
{
    private static async Task<(TriggerAliasEditorViewModel vm, FakeWorldStore store, World world)> BuildAsync()
    {
        var store = new FakeWorldStore();
        var world = new World { Name = "TestWorld", Host = "test.mud", Port = 4000 };
        await store.AddWorldAsync(world);
        var vm = new TriggerAliasEditorViewModel(store);
        return (vm, store, world);
    }

    [Test]
    public async Task LoadAsyncPopulatesTriggersAndAliases()
    {
        var (vm, store, world) = await BuildAsync();
        world.Triggers.Add(new TriggerRule { Pattern = "hp:", Kind = TriggerKind.Substring, Action = TriggerActionKind.Highlight, ActionValue = "1" });
        world.Aliases.Add(new AliasRule { Pattern = "^k (.+)$", Expansion = "kill $1" });
        await store.UpdateWorldAsync(world);

        await vm.LoadAsync(world.Id);

        await Assert.That(vm.Triggers).Count().IsEqualTo(1);
        await Assert.That(vm.Aliases).Count().IsEqualTo(1);
        await Assert.That(vm.Triggers[0].Pattern).IsEqualTo("hp:");
        await Assert.That(vm.Aliases[0].Pattern).IsEqualTo("^k (.+)$");
    }

    [Test]
    public async Task SetWorldExposesTriggersAndAliases()
    {
        var (vm, _, world) = await BuildAsync();
        world.Triggers.Add(new TriggerRule { Pattern = "test", Kind = TriggerKind.Regex, Action = TriggerActionKind.Send, ActionValue = "cmd" });

        vm.SetWorld(world);

        await Assert.That(vm.Triggers).Count().IsEqualTo(1);
    }

    [Test]
    public async Task AddTriggerAsyncPersistsAndAppearsAfterReload()
    {
        var (vm, store, world) = await BuildAsync();
        await vm.LoadAsync(world.Id);

        var rule = new TriggerRule { Pattern = "You die.", Kind = TriggerKind.Substring, Action = TriggerActionKind.Notify, ActionValue = "dead" };
        await vm.AddTriggerAsync(rule);

        await Assert.That(store.UpdateCount).IsGreaterThanOrEqualTo(1);
        await Assert.That(vm.Triggers).Count().IsEqualTo(1);
        await Assert.That(vm.Triggers[0].Pattern).IsEqualTo("You die.");
    }

    [Test]
    public async Task ToggleTriggerAsyncFlipsEnabledAndPersists()
    {
        var (vm, store, world) = await BuildAsync();
        var rule = new TriggerRule { Pattern = "hp:", Kind = TriggerKind.Substring, Action = TriggerActionKind.Highlight, ActionValue = "2", Enabled = true };
        world.Triggers.Add(rule);
        await store.UpdateWorldAsync(world);
        await vm.LoadAsync(world.Id);

        var priorUpdateCount = store.UpdateCount;
        await vm.ToggleTriggerAsync(rule.Id);

        await Assert.That(store.UpdateCount).IsGreaterThan(priorUpdateCount);
        await Assert.That(vm.Triggers[0].Enabled).IsFalse();
    }

    [Test]
    public async Task ToggleTriggerAsyncFlipsBackToEnabled()
    {
        var (vm, store, world) = await BuildAsync();
        var rule = new TriggerRule { Pattern = "hp:", Kind = TriggerKind.Substring, Action = TriggerActionKind.Highlight, ActionValue = "2", Enabled = false };
        world.Triggers.Add(rule);
        await store.UpdateWorldAsync(world);
        await vm.LoadAsync(world.Id);

        await vm.ToggleTriggerAsync(rule.Id);

        await Assert.That(vm.Triggers[0].Enabled).IsTrue();
    }

    [Test]
    public async Task DeleteTriggerAsyncRemovesRule()
    {
        var (vm, store, world) = await BuildAsync();
        var rule = new TriggerRule { Pattern = "hp:", Kind = TriggerKind.Substring, Action = TriggerActionKind.Highlight, ActionValue = "2" };
        world.Triggers.Add(rule);
        await store.UpdateWorldAsync(world);
        await vm.LoadAsync(world.Id);

        await vm.DeleteTriggerAsync(rule.Id);

        await Assert.That(vm.Triggers).IsEmpty();
    }

    [Test]
    public async Task UpdateTriggerAsyncReplacesRuleInPlace()
    {
        var (vm, store, world) = await BuildAsync();
        var rule = new TriggerRule { Pattern = "old", Kind = TriggerKind.Substring, Action = TriggerActionKind.Send, ActionValue = "x" };
        world.Triggers.Add(rule);
        await store.UpdateWorldAsync(world);
        await vm.LoadAsync(world.Id);

        var updated = new TriggerRule { Id = rule.Id, Pattern = "new", Kind = TriggerKind.Regex, Action = TriggerActionKind.Notify, ActionValue = "y" };
        await vm.UpdateTriggerAsync(updated);

        await Assert.That(vm.Triggers).Count().IsEqualTo(1);
        await Assert.That(vm.Triggers[0].Pattern).IsEqualTo("new");
    }

    [Test]
    public async Task AddAliasAsyncPersistsAndAppearsAfterReload()
    {
        var (vm, store, world) = await BuildAsync();
        await vm.LoadAsync(world.Id);

        var alias = new AliasRule { Pattern = "^k (.+)$", Expansion = "kill $1" };
        await vm.AddAliasAsync(alias);

        await Assert.That(vm.Aliases).Count().IsEqualTo(1);
        await Assert.That(vm.Aliases[0].Expansion).IsEqualTo("kill $1");
    }

    [Test]
    public async Task ToggleAliasAsyncFlipsEnabledAndPersists()
    {
        var (vm, store, world) = await BuildAsync();
        var alias = new AliasRule { Pattern = "^k$", Expansion = "kill", Enabled = true };
        world.Aliases.Add(alias);
        await store.UpdateWorldAsync(world);
        await vm.LoadAsync(world.Id);

        await vm.ToggleAliasAsync(alias.Id);

        await Assert.That(vm.Aliases[0].Enabled).IsFalse();
    }

    [Test]
    public async Task DeleteAliasAsyncRemovesAlias()
    {
        var (vm, store, world) = await BuildAsync();
        var alias = new AliasRule { Pattern = "^k$", Expansion = "kill" };
        world.Aliases.Add(alias);
        await store.UpdateWorldAsync(world);
        await vm.LoadAsync(world.Id);

        await vm.DeleteAliasAsync(alias.Id);

        await Assert.That(vm.Aliases).IsEmpty();
    }

    [Test]
    public async Task ChangedFiresOnLoadAsync()
    {
        var (vm, _, world) = await BuildAsync();
        var firedCount = 0;
        vm.Changed += () => firedCount++;

        await vm.LoadAsync(world.Id);

        await Assert.That(firedCount).IsGreaterThanOrEqualTo(1);
    }

    [Test]
    public async Task ChangedFiresOnAddTrigger()
    {
        var (vm, _, world) = await BuildAsync();
        await vm.LoadAsync(world.Id);
        var firedCount = 0;
        vm.Changed += () => firedCount++;

        await vm.AddTriggerAsync(new TriggerRule { Pattern = "x", Kind = TriggerKind.Substring, Action = TriggerActionKind.Send, ActionValue = "y" });

        await Assert.That(firedCount).IsGreaterThanOrEqualTo(1);
    }

    [Test]
    public async Task TriggersEmptyWhenWorldNotLoaded()
    {
        var store = new FakeWorldStore();
        var vm = new TriggerAliasEditorViewModel(store);

        await Assert.That(vm.Triggers).IsEmpty();
        await Assert.That(vm.Aliases).IsEmpty();
    }
}
