using SharpClient.Core.Domain;
using SharpClient.Core.Presentation;
using SharpClient.Core.Sessions;
using SharpClient.Tests.Fakes;

namespace SharpClient.Tests.Presentation;

public sealed class WorldManagerViewModelTests
{
    private static (WorldManagerViewModel vm, FakeWorldStore store, FakeSecretStore secrets, SessionManager sessions, FakeSessionLauncher launcher) Build()
    {
        var store = new FakeWorldStore();
        var secrets = new FakeSecretStore();
        var sessions = new SessionManager();
        var launcher = new FakeSessionLauncher();
        var vm = new WorldManagerViewModel(store, secrets, sessions, launcher);
        return (vm, store, secrets, sessions, launcher);
    }

    [Test]
    public async Task LoadAsyncPopulatesWorlds()
    {
        var (vm, store, _, _, _) = Build();
        await store.AddWorldAsync(new World { Name = "Sindome", Host = "sindome.org", Port = 5555 });

        await vm.LoadAsync();

        await Assert.That(vm.Worlds).Count().IsEqualTo(1);
        await Assert.That(vm.Worlds[0].Name).IsEqualTo("Sindome");
        await Assert.That(vm.HasWorlds).IsTrue();
    }

    [Test]
    public async Task HasWorldsIsFalseWhenEmpty()
    {
        var (vm, _, _, _, _) = Build();
        await vm.LoadAsync();
        await Assert.That(vm.HasWorlds).IsFalse();
    }

    [Test]
    public async Task AddWorldPersistsAndFiresChanged()
    {
        var (vm, store, _, _, _) = Build();
        var changed = 0;
        vm.Changed += () => changed++;

        await vm.AddWorldAsync("Aardwolf", "aardmud.org", 23);

        await Assert.That(store.AddCount).IsEqualTo(1);
        await Assert.That(vm.Worlds).Count().IsEqualTo(1);
        await Assert.That(vm.Worlds[0].Host).IsEqualTo("aardmud.org");
        await Assert.That(changed).IsGreaterThanOrEqualTo(1);
    }

    [Test]
    public async Task AddCharacterWithConnectStringStoresSecretAndNotPlaintext()
    {
        var (vm, _, secrets, _, _) = Build();
        await vm.AddWorldAsync("Sindome", "sindome.org", 5555);
        var world = vm.Worlds[0];

        const string connect = "connect Vesper hunter2";
        await vm.AddCharacterAsync(world, "Vesper", connect);

        var loaded = vm.Worlds[0];
        var character = loaded.Characters.Single();

        // Key set on the domain, plaintext stored only via the secret store.
        await Assert.That(character.ConnectSecretKey).IsNotNull();
        await Assert.That(await secrets.GetAsync(character.ConnectSecretKey!)).IsEqualTo(connect);

        // Plaintext must NOT appear in any domain text field.
        await Assert.That(character.Name).IsEqualTo("Vesper");
        await Assert.That(character.Name).DoesNotContain("hunter2");
        await Assert.That(character.ConnectSecretKey!).DoesNotContain("hunter2");
        await Assert.That(loaded.Name).DoesNotContain("hunter2");
        await Assert.That(loaded.Host).DoesNotContain("hunter2");
    }

    [Test]
    public async Task AddCharacterWithoutConnectStringLeavesKeyNull()
    {
        var (vm, _, _, _, _) = Build();
        await vm.AddWorldAsync("Sindome", "sindome.org", 5555);

        await vm.AddCharacterAsync(vm.Worlds[0], "Nameless", null);

        await Assert.That(vm.Worlds[0].Characters.Single().ConnectSecretKey).IsNull();
    }

    [Test]
    public async Task DeleteWorldRemovesIt()
    {
        var (vm, _, _, _, _) = Build();
        await vm.AddWorldAsync("Sindome", "sindome.org", 5555);
        var id = vm.Worlds[0].Id;

        await vm.DeleteWorldAsync(id);

        await Assert.That(vm.Worlds).IsEmpty();
        await Assert.That(vm.HasWorlds).IsFalse();
    }

    [Test]
    public async Task DeleteCharacterRemovesItAndSecret()
    {
        var (vm, _, secrets, _, _) = Build();
        await vm.AddWorldAsync("Sindome", "sindome.org", 5555);
        await vm.AddCharacterAsync(vm.Worlds[0], "Vesper", "connect Vesper pw");
        var character = vm.Worlds[0].Characters.Single();
        var key = character.ConnectSecretKey!;

        await vm.DeleteCharacterAsync(vm.Worlds[0], character);

        await Assert.That(vm.Worlds[0].Characters).IsEmpty();
        await Assert.That(await secrets.GetAsync(key)).IsNull();
    }

    [Test]
    public async Task ConnectAsyncLaunchesAndAddsToManager()
    {
        var (vm, _, _, sessions, launcher) = Build();
        await vm.AddWorldAsync("Sindome", "sindome.org", 5555);
        await vm.AddCharacterAsync(vm.Worlds[0], "Vesper", null);
        var world = vm.Worlds[0];
        var character = world.Characters.Single();

        await vm.ConnectAsync(world, character);

        await Assert.That(launcher.LaunchCount).IsEqualTo(1);
        await Assert.That(launcher.LastWorld).IsEqualTo(world);
        await Assert.That(launcher.LastCharacter).IsEqualTo(character);
        await Assert.That(sessions.Sessions).Contains(launcher.Session);
    }

    [Test]
    public async Task UpdateWorldAsyncRenameAndPersists()
    {
        var (vm, store, _, _, _) = Build();
        await vm.AddWorldAsync("OldName", "mud.org", 4000);
        var world = vm.Worlds[0];
        var priorUpdateCount = store.UpdateCount;

        world.Name = "NewName";
        await vm.UpdateWorldAsync(world);

        await Assert.That(store.UpdateCount).IsGreaterThan(priorUpdateCount);
        await Assert.That(vm.Worlds[0].Name).IsEqualTo("NewName");
    }

    [Test]
    public async Task UpdateCharacterAsyncReusesExistingSecretKeyAndUpdatesValue()
    {
        var (vm, _, secrets, _, _) = Build();
        await vm.AddWorldAsync("Sindome", "sindome.org", 5555);
        await vm.AddCharacterAsync(vm.Worlds[0], "Vesper", "connect Vesper oldpass");
        var character = vm.Worlds[0].Characters.Single();
        var originalKey = character.ConnectSecretKey;

        await Assert.That(originalKey).IsNotNull();

        await vm.UpdateCharacterAsync(vm.Worlds[0], character, "Vesper", "connect Vesper newpass");
        var updatedCharacter = vm.Worlds[0].Characters.Single();

        // Key must be reused — not regenerated.
        await Assert.That(updatedCharacter.ConnectSecretKey).IsEqualTo(originalKey);
        // The secret value is updated at that key.
        await Assert.That(await secrets.GetAsync(originalKey!)).IsEqualTo("connect Vesper newpass");
    }

    [Test]
    public async Task DeleteWorldAsyncRemovesSecretsForAllCharacters()
    {
        var (vm, _, secrets, _, _) = Build();
        await vm.AddWorldAsync("Sindome", "sindome.org", 5555);
        await vm.AddCharacterAsync(vm.Worlds[0], "Vesper", "connect Vesper pw1");
        await vm.AddCharacterAsync(vm.Worlds[0], "Ghost",  "connect Ghost pw2");
        var world = vm.Worlds[0];
        var key1 = world.Characters[0].ConnectSecretKey!;
        var key2 = world.Characters[1].ConnectSecretKey!;

        await vm.DeleteWorldAsync(world.Id);

        await Assert.That(vm.Worlds).IsEmpty();
        await Assert.That(await secrets.GetAsync(key1)).IsNull();
        await Assert.That(await secrets.GetAsync(key2)).IsNull();
    }
}
