using Microsoft.Extensions.DependencyInjection;
using SharpClient.Core.Connection;
using SharpClient.Core.Domain;
using SharpClient.Core.Presentation;
using SharpClient.Core.Sessions;
using SharpClient.Core.Triggers;
using SharpClient.Tests.Connection;
using SharpClient.Tests.Fakes;
using TelnetNegotiationCore.Builders;

namespace SharpClient.Tests.Sessions;

/// <summary>
/// Integration tests for the runtime wiring stream: identity, world correlation,
/// NAWS, alias expansion, trigger application, history population, and error state.
/// </summary>
public sealed class RuntimeWiringTests
{
    private ServiceProvider? _serviceProvider;

    private ITelnetInterpreterFactory CreateFactory()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTelnetClient();
        _serviceProvider = services.BuildServiceProvider();
        return _serviceProvider.GetRequiredService<ITelnetInterpreterFactory>();
    }

    [After(Test)]
    public async Task TearDown()
    {
        if (_serviceProvider is not null)
        {
            await _serviceProvider.DisposeAsync();
            _serviceProvider = null;
        }
    }

    // ── Task 1: ISession identity ─────────────────────────────────────────────

    [Test]
    public async Task SessionExposesWorldIdAndCharacterId()
    {
        var worldId = Guid.NewGuid();
        var charId = Guid.NewGuid();
        var conn = new FakeTelnetConnection();

        await using var session = new Session(conn, "Vesper", "Sindome", worldId, charId);

        await Assert.That(session.WorldId).IsEqualTo(worldId);
        await Assert.That(session.CharacterId).IsEqualTo(charId);
    }

    [Test]
    public async Task SessionDefaultIdentityIsEmpty()
    {
        var conn = new FakeTelnetConnection();
        await using var session = new Session(conn);

        await Assert.That(session.WorldId).IsEqualTo(Guid.Empty);
        await Assert.That(session.CharacterId).IsEqualTo(Guid.Empty);
    }

    [Test]
    public async Task ISessionDefaultIdentityDimIsEmpty()
    {
        // Ensure the DIM defaults hold for types that implement ISession without overriding.
        ISession session = new FakeSession();

        await Assert.That(session.WorldId).IsEqualTo(Guid.Empty);
        await Assert.That(session.CharacterId).IsEqualTo(Guid.Empty);
    }

    // ── Task 2: World↔session correlation ────────────────────────────────────

    [Test]
    public async Task ActiveSessionForMatchesByCharacterId()
    {
        var charId = Guid.NewGuid();
        var worldStore = new FakeWorldStore();
        var secrets = new FakeSecretStore();
        var sessions = new SessionManager();
        var launcher = new FakeSessionLauncher();
        var vm = new WorldManagerViewModel(worldStore, secrets, sessions, launcher);

        // Seed a world with one character.
        var world = new World { Name = "Sindome", Host = "localhost", Port = 23 };
        var character = new Character { Id = charId, WorldId = world.Id, Name = "Vesper" };
        world.Characters.Add(character);
        await worldStore.AddWorldAsync(world);
        await vm.LoadAsync();

        // Add a fake session whose CharacterId matches.
        var fakeSession = new FakeSession { CharacterId = charId };
        sessions.Add(fakeSession);

        var found = vm.ActiveSessionFor(character);

        await Assert.That(found).IsEqualTo(fakeSession);
    }

    [Test]
    public async Task ActiveSessionForFallsBackToName()
    {
        var worldStore = new FakeWorldStore();
        var secrets = new FakeSecretStore();
        var sessions = new SessionManager();
        var launcher = new FakeSessionLauncher();
        var vm = new WorldManagerViewModel(worldStore, secrets, sessions, launcher);

        var world = new World { Name = "MUSHWorld", Host = "localhost", Port = 23 };
        var character = new Character { WorldId = world.Id, Name = "Ghost" };
        world.Characters.Add(character);
        await worldStore.AddWorldAsync(world);
        await vm.LoadAsync();

        // Session has empty CharacterId (old session) but matching name.
        var fakeSession = new FakeSession { CharacterName = "Ghost" };
        sessions.Add(fakeSession);

        var found = vm.ActiveSessionFor(character);

        await Assert.That(found).IsEqualTo(fakeSession);
    }

    [Test]
    public async Task ActiveSessionForReturnsNullWhenNoMatch()
    {
        var worldStore = new FakeWorldStore();
        var secrets = new FakeSecretStore();
        var sessions = new SessionManager();
        var launcher = new FakeSessionLauncher();
        var vm = new WorldManagerViewModel(worldStore, secrets, sessions, launcher);

        var world = new World { Name = "W", Host = "localhost", Port = 23 };
        var character = new Character { WorldId = world.Id, Name = "Nobody" };
        world.Characters.Add(character);
        await worldStore.AddWorldAsync(world);
        await vm.LoadAsync();

        var found = vm.ActiveSessionFor(character);

        await Assert.That(found).IsNull();
    }

    [Test]
    public async Task IsWorldLiveTrueWhenCharacterHasSession()
    {
        var charId = Guid.NewGuid();
        var worldStore = new FakeWorldStore();
        var secrets = new FakeSecretStore();
        var sessions = new SessionManager();
        var launcher = new FakeSessionLauncher();
        var vm = new WorldManagerViewModel(worldStore, secrets, sessions, launcher);

        var world = new World { Name = "Sindome", Host = "localhost", Port = 23 };
        var character = new Character { Id = charId, WorldId = world.Id, Name = "Vesper" };
        world.Characters.Add(character);
        await worldStore.AddWorldAsync(world);
        await vm.LoadAsync();

        sessions.Add(new FakeSession { CharacterId = charId });

        await Assert.That(vm.IsWorldLive(vm.Worlds[0])).IsTrue();
    }

    [Test]
    public async Task IsWorldLiveFalseWhenNoCharacterHasSession()
    {
        var worldStore = new FakeWorldStore();
        var secrets = new FakeSecretStore();
        var sessions = new SessionManager();
        var launcher = new FakeSessionLauncher();
        var vm = new WorldManagerViewModel(worldStore, secrets, sessions, launcher);

        var world = new World { Name = "Sindome", Host = "localhost", Port = 23 };
        world.Characters.Add(new Character { WorldId = world.Id, Name = "Vesper" });
        await worldStore.AddWorldAsync(world);
        await vm.LoadAsync();

        await Assert.That(vm.IsWorldLive(vm.Worlds[0])).IsFalse();
    }

    // ── Task 3: NAWS forwarding ───────────────────────────────────────────────

    [Test]
    public async Task SendWindowSizeAsyncForwardsNawsToConnection()
    {
        var conn = new FakeTelnetConnection();
        await using var session = new Session(conn);

        await session.SendWindowSizeAsync(80, 24);

        await Assert.That(conn.NawsSent.Count).IsEqualTo(1);
        await Assert.That(conn.NawsSent[0].Width).IsEqualTo(80);
        await Assert.That(conn.NawsSent[0].Height).IsEqualTo(24);
    }

    // ── Task 4: Alias expansion ───────────────────────────────────────────────

    [Test]
    public async Task SendAsyncExpandsAliasBeforeSending()
    {
        var conn = new FakeTelnetConnection();
        var aliasRules = new List<AliasRule>
        {
            new() { Pattern = @"^k (.+)$", Expansion = "kill $1", Enabled = true },
        };
        var engine = new AliasEngine();

        await using var session = new Session(conn, aliasEngine: engine, aliasRules: aliasRules);

        await session.SendAsync("k goblin");

        await Assert.That(conn.Sent).Contains("kill goblin");
    }

    [Test]
    public async Task SendAsyncPassesThroughWhenNoMatchingAlias()
    {
        var conn = new FakeTelnetConnection();
        var aliasRules = new List<AliasRule>
        {
            new() { Pattern = @"^k (.+)$", Expansion = "kill $1", Enabled = true },
        };
        var engine = new AliasEngine();

        await using var session = new Session(conn, aliasEngine: engine, aliasRules: aliasRules);

        await session.SendAsync("look");

        await Assert.That(conn.Sent).Contains("look");
    }

    // ── Task 5: Trigger application ───────────────────────────────────────────

    [Test]
    public async Task IncomingLineAppliesTriggerSendCommand()
    {
        var conn = new FakeTelnetConnection();
        var triggerRules = new List<TriggerRule>
        {
            new()
            {
                Kind = TriggerKind.Substring,
                Pattern = "You are hungry",
                Action = TriggerActionKind.Send,
                ActionValue = "eat bread",
                Enabled = true,
            },
        };
        var engine = new TriggerEngine();
        var notifier = new FakeNotifier();

        await using var session = new Session(
            conn,
            triggerEngine: engine,
            triggerRules: triggerRules,
            notifier: notifier);

        conn.Emit("You are hungry");

        // Allow the async void handler to drain (FakeTelnetConnection.SendAsync
        // returns Task.CompletedTask so all awaits complete synchronously).
        await Task.Yield();

        await Assert.That(conn.Sent).Contains("eat bread");
    }

    [Test]
    public async Task IncomingLineFiresTriggerNotification()
    {
        var conn = new FakeTelnetConnection();
        var triggerRules = new List<TriggerRule>
        {
            new()
            {
                Kind = TriggerKind.Substring,
                Pattern = "tells you",
                Action = TriggerActionKind.Notify,
                ActionValue = "Incoming tell",
                Enabled = true,
            },
        };
        var engine = new TriggerEngine();
        var notifier = new FakeNotifier();

        await using var session = new Session(
            conn,
            triggerEngine: engine,
            triggerRules: triggerRules,
            notifier: notifier);

        conn.Emit("Someone tells you: hello!");
        await Task.Yield();

        await Assert.That(notifier.Messages).Contains("Incoming tell");
    }

    [Test]
    public async Task IncomingLineUsesAnsiParserWhenNoTriggerEngine()
    {
        var conn = new FakeTelnetConnection();
        await using var session = new Session(conn);

        conn.Emit("plain text");

        await Assert.That(session.Scrollback.Count).IsEqualTo(1);
        await Assert.That(session.Scrollback[0].Segments[0].Text).IsEqualTo("plain text");
    }

    // ── Task 6: Session history ───────────────────────────────────────────────

    [Test]
    public async Task IncomingLineCallsHistoryAppendAsync()
    {
        var charId = Guid.NewGuid();
        var conn = new FakeTelnetConnection();
        var history = new FakeSessionHistory();

        await using var session = new Session(
            conn,
            characterId: charId,
            history: history);

        conn.Emit("The goblin attacks!");
        await Task.Yield();

        await Assert.That(history.Appended.Count).IsEqualTo(1);
        await Assert.That(history.Appended[0].CharacterId).IsEqualTo(charId);
        await Assert.That(history.Appended[0].Line).IsEqualTo("The goblin attacks!");
    }

    // ── Task 7: Error state on failed connect ─────────────────────────────────

    [Test]
    public async Task ConnectToUnreachableHostSetsErrorState()
    {
        await using var connection = new TelnetConnection(CreateFactory());

        var states = new List<ConnectionState>();
        connection.StateChanged += states.Add;

        await Assert.ThrowsAsync<Exception>(async () =>
            await connection.ConnectAsync("127.0.0.1", 1));

        await Assert.That(states).Contains(ConnectionState.Error);
        await Assert.That(connection.State).IsEqualTo(ConnectionState.Error);
    }
}
