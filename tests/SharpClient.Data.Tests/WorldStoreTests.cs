using SharpClient.Core.Domain;
using SharpClient.Core.Platform;
using SharpClient.Data;

namespace SharpClient.Data.Tests;

/// <summary>
/// Minimal IAppStorage that provides a unique temp SQLite file per instance.
/// Not IDisposable — lifecycle managed by the test class.
/// </summary>
internal sealed class TempFileAppStorage : IAppStorage
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".db");

    public string GetDatabasePath() => _path;

    public void Delete()
    {
        if (File.Exists(_path)) File.Delete(_path);
    }
}

public sealed class WorldStoreTests
{
    // TempFileAppStorage is not IDisposable, so CA1001 is not triggered.
    private TempFileAppStorage _storage = null!;

    [Before(Test)]
    public void Setup() => _storage = new TempFileAppStorage();

    [After(Test)]
    public void Teardown() => _storage.Delete();

    // ── Add + Get ────────────────────────────────────────────────────────────

    [Test]
    public async Task AddWorldThenGetWorldsReturnsFullyPopulatedGraph()
    {
        var world = BuildSampleWorld();

        await using var db = new AppDbContext(_storage);
        var store = new WorldStore(db);

        await store.AddWorldAsync(world);
        var worlds = await store.GetWorldsAsync();

        await Assert.That(worlds).Count().IsEqualTo(1);

        var loaded = worlds[0];
        await Assert.That(loaded.Name).IsEqualTo("Test World");
        await Assert.That(loaded.Host).IsEqualTo("mush.example.com");
        await Assert.That(loaded.Port).IsEqualTo(4201);

        await Assert.That(loaded.Triggers).Count().IsEqualTo(1);
        await Assert.That(loaded.Aliases).Count().IsEqualTo(1);
        await Assert.That(loaded.Characters).Count().IsEqualTo(2);

        var charA = loaded.Characters.First(c => c.Name == "CharA");
        await Assert.That(charA.ConnectSecretKey).IsEqualTo("key-secret-charA");
        await Assert.That(charA.Triggers).Count().IsEqualTo(1);
        await Assert.That(charA.Aliases).Count().IsEqualTo(1);

        var charB = loaded.Characters.First(c => c.Name == "CharB");
        await Assert.That(charB.Triggers).Count().IsEqualTo(0);
        await Assert.That(charB.Aliases).Count().IsEqualTo(0);
    }

    [Test]
    public async Task AddWorldRoundTripsTriggerRuleFields()
    {
        var world = new World { Name = "W", Host = "h", Port = 1 };
        world.Triggers.Add(new TriggerRule
        {
            Kind = TriggerKind.Regex,
            Pattern = @"\bfoo\b",
            Action = TriggerActionKind.Highlight,
            ActionValue = "#FF0000",
            Enabled = false,
        });

        await using var db = new AppDbContext(_storage);
        var store = new WorldStore(db);

        await store.AddWorldAsync(world);
        var loaded = (await store.GetWorldsAsync())[0].Triggers[0];

        await Assert.That(loaded.Kind).IsEqualTo(TriggerKind.Regex);
        await Assert.That(loaded.Pattern).IsEqualTo(@"\bfoo\b");
        await Assert.That(loaded.Action).IsEqualTo(TriggerActionKind.Highlight);
        await Assert.That(loaded.ActionValue).IsEqualTo("#FF0000");
        await Assert.That(loaded.Enabled).IsFalse();
    }

    [Test]
    public async Task AddWorldRoundTripsAliasRuleFields()
    {
        var world = new World { Name = "W", Host = "h", Port = 1 };
        world.Aliases.Add(new AliasRule { Pattern = @"^k (.+)$", Expansion = "kill $1", Enabled = true });

        await using var db = new AppDbContext(_storage);
        var store = new WorldStore(db);

        await store.AddWorldAsync(world);
        var loaded = (await store.GetWorldsAsync())[0].Aliases[0];

        await Assert.That(loaded.Pattern).IsEqualTo(@"^k (.+)$");
        await Assert.That(loaded.Expansion).IsEqualTo("kill $1");
        await Assert.That(loaded.Enabled).IsTrue();
    }

    [Test]
    public async Task AddWorldRoundTripsConnectSecretKey()
    {
        var world = new World { Name = "W", Host = "h", Port = 1 };
        world.Characters.Add(new Character
        {
            WorldId = world.Id,
            Name = "Hero",
            ConnectSecretKey = "vault:hero-key",
        });

        await using var db = new AppDbContext(_storage);
        var store = new WorldStore(db);

        await store.AddWorldAsync(world);
        var loaded = (await store.GetWorldsAsync())[0].Characters[0];

        await Assert.That(loaded.ConnectSecretKey).IsEqualTo("vault:hero-key");
    }

    // ── Update ───────────────────────────────────────────────────────────────

    [Test]
    public async Task UpdateWorldRenameAndAddCharacterReflectedInGetWorlds()
    {
        var world = BuildSampleWorld();

        await using var db = new AppDbContext(_storage);
        var store = new WorldStore(db);

        await store.AddWorldAsync(world);

        world.Name = "Updated World";
        world.Characters.Add(new Character { WorldId = world.Id, Name = "CharC" });

        await store.UpdateWorldAsync(world);
        var worlds = await store.GetWorldsAsync();

        await Assert.That(worlds).Count().IsEqualTo(1);
        await Assert.That(worlds[0].Name).IsEqualTo("Updated World");
        await Assert.That(worlds[0].Characters).Count().IsEqualTo(3);
    }

    [Test]
    public async Task UpdateWorldRemoveCharacterNotReturnedByGetWorlds()
    {
        var world = BuildSampleWorld();

        await using var db = new AppDbContext(_storage);
        var store = new WorldStore(db);

        await store.AddWorldAsync(world);

        var charB = world.Characters.First(c => c.Name == "CharB");
        world.Characters.Remove(charB);

        await store.UpdateWorldAsync(world);
        var worlds = await store.GetWorldsAsync();

        await Assert.That(worlds[0].Characters).Count().IsEqualTo(1);
        await Assert.That(worlds[0].Characters[0].Name).IsEqualTo("CharA");
    }

    // ── Delete ───────────────────────────────────────────────────────────────

    [Test]
    public async Task DeleteWorldGetWorldsReturnsEmpty()
    {
        var world = BuildSampleWorld();

        await using var db = new AppDbContext(_storage);
        var store = new WorldStore(db);

        await store.AddWorldAsync(world);
        await store.DeleteWorldAsync(world.Id);
        var worlds = await store.GetWorldsAsync();

        await Assert.That(worlds).Count().IsEqualTo(0);
    }

    [Test]
    public async Task DeleteWorldCascadesChildrenNoOrphanRows()
    {
        var world = BuildSampleWorld();

        await using (var db = new AppDbContext(_storage))
        {
            var store = new WorldStore(db);
            await store.AddWorldAsync(world);
            await store.DeleteWorldAsync(world.Id);
        }

        // Re-open DB to verify no orphan rows remain
        await using var db2 = new AppDbContext(_storage);
        var store2 = new WorldStore(db2);
        var worlds = await store2.GetWorldsAsync();
        await Assert.That(worlds).Count().IsEqualTo(0);

        await Assert.That(db2.Characters.Count()).IsEqualTo(0);
        await Assert.That(db2.TriggerRules.Count()).IsEqualTo(0);
        await Assert.That(db2.AliasRules.Count()).IsEqualTo(0);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static World BuildSampleWorld()
    {
        var world = new World
        {
            Name = "Test World",
            Host = "mush.example.com",
            Port = 4201,
        };

        world.Triggers.Add(new TriggerRule
        {
            Kind = TriggerKind.Substring,
            Pattern = "arrival",
            Action = TriggerActionKind.Notify,
            ActionValue = "Someone arrived",
            Enabled = true,
        });

        world.Aliases.Add(new AliasRule
        {
            Pattern = @"^ga$",
            Expansion = "go away",
            Enabled = true,
        });

        var charA = new Character
        {
            WorldId = world.Id,
            Name = "CharA",
            ConnectSecretKey = "key-secret-charA",
        };
        charA.Triggers.Add(new TriggerRule
        {
            Kind = TriggerKind.Regex,
            Pattern = @"\battack\b",
            Action = TriggerActionKind.Send,
            ActionValue = "dodge",
            Enabled = true,
        });
        charA.Aliases.Add(new AliasRule
        {
            Pattern = @"^atk (.+)$",
            Expansion = "attack $1",
            Enabled = true,
        });

        var charB = new Character { WorldId = world.Id, Name = "CharB" };

        world.Characters.Add(charA);
        world.Characters.Add(charB);

        return world;
    }
}
