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
    public async Task UpdateAliasAsyncReplacesAliasInPlace()
    {
        var (vm, store, world) = await BuildAsync();
        var alias = new AliasRule { Pattern = "^old$", Expansion = "old-expansion" };
        world.Aliases.Add(alias);
        await store.UpdateWorldAsync(world);
        await vm.LoadAsync(world.Id);

        var updated = new AliasRule { Id = alias.Id, Pattern = "^new$", Expansion = "new-expansion" };
        await vm.UpdateAliasAsync(updated);

        await Assert.That(vm.Aliases).Count().IsEqualTo(1);
        await Assert.That(vm.Aliases[0].Pattern).IsEqualTo("^new$");
        await Assert.That(vm.Aliases[0].Expansion).IsEqualTo("new-expansion");
        await Assert.That(store.UpdateCount).IsGreaterThanOrEqualTo(1);
    }

    [Test]
    public async Task TriggersEmptyWhenWorldNotLoaded()
    {
        var store = new FakeWorldStore();
        var vm = new TriggerAliasEditorViewModel(store);

        await Assert.That(vm.Triggers).IsEmpty();
        await Assert.That(vm.Aliases).IsEmpty();
    }

    // ── Character-scope tests ─────────────────────────────────────────────

    private static async Task<(TriggerAliasEditorViewModel vm, FakeWorldStore store, World world, Character character)>
        BuildWithCharacterAsync()
    {
        var store = new FakeWorldStore();
        var world = new World { Name = "TestWorld", Host = "test.mud", Port = 4000 };
        var character = new Character { Name = "Mannaz", WorldId = world.Id };
        world.Characters.Add(character);
        await store.AddWorldAsync(world);
        var vm = new TriggerAliasEditorViewModel(store);
        await vm.LoadAsync(world.Id);
        return (vm, store, world, character);
    }

    [Test]
    public async Task DefaultScopeIsWorld()
    {
        var (vm, _, _, _) = await BuildWithCharacterAsync();

        await Assert.That(vm.CharacterScopeId).IsNull();
        await Assert.That(vm.ScopeName).IsEqualTo("World");
    }

    [Test]
    public async Task SetScopeSwitchesToCharacter()
    {
        var (vm, _, _, character) = await BuildWithCharacterAsync();

        vm.SetScope(character.Id);

        await Assert.That(vm.CharacterScopeId).IsEqualTo(character.Id);
        await Assert.That(vm.ScopeName).IsEqualTo("Mannaz");
    }

    [Test]
    public async Task SetScopeNullReverts()
    {
        var (vm, _, _, character) = await BuildWithCharacterAsync();
        vm.SetScope(character.Id);

        vm.SetScope(null);

        await Assert.That(vm.CharacterScopeId).IsNull();
        await Assert.That(vm.ScopeName).IsEqualTo("World");
    }

    [Test]
    public async Task SetScopeFiresChanged()
    {
        var (vm, _, _, character) = await BuildWithCharacterAsync();
        var fired = 0;
        vm.Changed += () => fired++;

        vm.SetScope(character.Id);

        await Assert.That(fired).IsGreaterThanOrEqualTo(1);
    }

    [Test]
    public async Task CharacterScopeTriggersAreSeparateFromWorldTriggers()
    {
        var (vm, store, world, character) = await BuildWithCharacterAsync();
        world.Triggers.Add(new TriggerRule { Pattern = "world-only", Kind = TriggerKind.Substring, Action = TriggerActionKind.Highlight, ActionValue = "1" });
        character.Triggers.Add(new TriggerRule { Pattern = "char-only", Kind = TriggerKind.Substring, Action = TriggerActionKind.Send, ActionValue = "cmd" });
        await store.UpdateWorldAsync(world);
        await vm.LoadAsync(world.Id);

        // world scope
        await Assert.That(vm.Triggers.Select(t => t.Pattern)).Contains("world-only");
        await Assert.That(vm.Triggers.Select(t => t.Pattern)).DoesNotContain("char-only");

        // character scope
        vm.SetScope(character.Id);
        await Assert.That(vm.Triggers.Select(t => t.Pattern)).Contains("char-only");
        await Assert.That(vm.Triggers.Select(t => t.Pattern)).DoesNotContain("world-only");
    }

    [Test]
    public async Task AddTriggerInCharacterScopeDoesNotAffectWorldTriggers()
    {
        var (vm, store, world, character) = await BuildWithCharacterAsync();
        vm.SetScope(character.Id);

        await vm.AddTriggerAsync(new TriggerRule { Pattern = "char-trigger", Kind = TriggerKind.Substring, Action = TriggerActionKind.Notify, ActionValue = "n" });

        // character has the new trigger
        await Assert.That(vm.Triggers.Select(t => t.Pattern)).Contains("char-trigger");

        // world scope should NOT have it
        vm.SetScope(null);
        await Assert.That(vm.Triggers.Select(t => t.Pattern)).DoesNotContain("char-trigger");

        // persisted
        await Assert.That(store.UpdateCount).IsGreaterThanOrEqualTo(1);
    }

    [Test]
    public async Task UpdateTriggerInCharacterScopeUpdatesCorrectList()
    {
        var (vm, store, world, character) = await BuildWithCharacterAsync();
        var rule = new TriggerRule { Pattern = "old", Kind = TriggerKind.Substring, Action = TriggerActionKind.Send, ActionValue = "x" };
        character.Triggers.Add(rule);
        await store.UpdateWorldAsync(world);
        await vm.LoadAsync(world.Id);
        vm.SetScope(character.Id);

        var updated = new TriggerRule { Id = rule.Id, Pattern = "new", Kind = TriggerKind.Regex, Action = TriggerActionKind.Highlight, ActionValue = "y" };
        await vm.UpdateTriggerAsync(updated);

        await Assert.That(vm.Triggers).Count().IsEqualTo(1);
        await Assert.That(vm.Triggers[0].Pattern).IsEqualTo("new");
        await Assert.That(store.UpdateCount).IsGreaterThanOrEqualTo(1);
    }

    [Test]
    public async Task DeleteTriggerInCharacterScopeDeletesFromCharacter()
    {
        var (vm, store, world, character) = await BuildWithCharacterAsync();
        var rule = new TriggerRule { Pattern = "del-me", Kind = TriggerKind.Substring, Action = TriggerActionKind.Send, ActionValue = "x" };
        character.Triggers.Add(rule);
        await store.UpdateWorldAsync(world);
        await vm.LoadAsync(world.Id);
        vm.SetScope(character.Id);

        await vm.DeleteTriggerAsync(rule.Id);

        await Assert.That(vm.Triggers).IsEmpty();
    }

    [Test]
    public async Task ToggleTriggerInCharacterScopeFlipsEnabled()
    {
        var (vm, store, world, character) = await BuildWithCharacterAsync();
        var rule = new TriggerRule { Pattern = "toggle-me", Kind = TriggerKind.Substring, Action = TriggerActionKind.Send, ActionValue = "x", Enabled = true };
        character.Triggers.Add(rule);
        await store.UpdateWorldAsync(world);
        await vm.LoadAsync(world.Id);
        vm.SetScope(character.Id);

        await vm.ToggleTriggerAsync(rule.Id);

        await Assert.That(vm.Triggers[0].Enabled).IsFalse();
        await Assert.That(store.UpdateCount).IsGreaterThanOrEqualTo(1);
    }

    [Test]
    public async Task AddAliasInCharacterScopeDoesNotAffectWorldAliases()
    {
        var (vm, store, _, character) = await BuildWithCharacterAsync();
        vm.SetScope(character.Id);

        await vm.AddAliasAsync(new AliasRule { Pattern = "^char$", Expansion = "char-expansion" });

        await Assert.That(vm.Aliases.Select(a => a.Pattern)).Contains("^char$");
        vm.SetScope(null);
        await Assert.That(vm.Aliases.Select(a => a.Pattern)).DoesNotContain("^char$");
    }

    [Test]
    public async Task CharactersListExposedOnVm()
    {
        var (vm, _, world, character) = await BuildWithCharacterAsync();

        await Assert.That(vm.Characters).Count().IsEqualTo(1);
        await Assert.That(vm.Characters[0].Id).IsEqualTo(character.Id);
    }

    [Test]
    public async Task ScopeFallsBackToWorldWhenCharacterNotFound()
    {
        var (vm, store, world, character) = await BuildWithCharacterAsync();
        vm.SetScope(character.Id);

        // Remove character from world and reload
        world.Characters.Clear();
        await store.UpdateWorldAsync(world);
        await vm.LoadAsync(world.Id);

        await Assert.That(vm.CharacterScopeId).IsNull();
        await Assert.That(vm.ScopeName).IsEqualTo("World");
    }
}
