# World Manager Screen Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build SharpClient's World Manager screen — manage Worlds and their Characters, and Connect a character to open a live session — as an interface-based, testable Blazor feature.

**Architecture:** A UI-agnostic `WorldManagerViewModel` (Core) wraps `IWorldStore`/`ISecretStore`/`ISessionManager` and a new `ISessionLauncher` abstraction so the Connect flow never hard-depends on telnet sockets (the web host can't open them). Razor components in `SharpClient.UI` render the VM; the `SharpClient.Web` host wires real EF persistence, an in-memory secret store, and a `DemoSessionLauncher` that returns the existing `DemoSession`.

**Tech Stack:** .NET 10 (`net10.0`), C# (file-scoped namespaces, nullable + implicit usings global), Blazor Server, EF Core + SQLite, TUnit (logic/VM tests), bUnit (component tests).

## Global Constraints

(Every task's requirements implicitly include this section. Values copied verbatim from the spec.)

- Target framework: `net10.0`.
- `TreatWarningsAsErrors=true` is set for ALL projects (via `Directory.Build.props`) → every project MUST build with **0 warnings**. Unused `using` directives, missing `[Parameter]` defaults, nullable mismatches, and unsealed-where-sensible analyzer hits all fail the build.
- `Nullable=enable` and `ImplicitUsings=enable` are GLOBAL — do NOT redeclare them in any `.csproj`, and do NOT add `using System;`, `using System.Threading.Tasks;`, `using System.Collections.Generic;` etc. (already implicit).
- File-scoped namespaces in every `.cs` file.
- `sealed` on every concrete class where sensible (all classes in this plan are `sealed`).
- Tests: TUnit (`[Test]`, `[Before(Test)]`, `[After(Test)]`, `await Assert.That(...)`) for logic/VM; bUnit for components.
- EF `WorldStore` interaction in tests uses a **temp SQLite file deleted in teardown** — mirror `tests/SharpClient.Data.Tests/WorldStoreTests.cs` (`TempFileAppStorage`). The VM tests in this plan use an in-memory `FakeWorldStore` instead (allowed by the spec) — no SQLite there.
- Keep `WorldManagerViewModel` UI-agnostic: NO Blazor / `Microsoft.AspNetCore.*` references in `SharpClient.Core`.
- Existing tests MUST stay green: `SharpClient.Tests` = 75, `SharpClient.UI.Tests` = 8, `SharpClient.Data.Tests` = 16.
- Secrets rule: the connect string is a SECRET. Store it via `ISecretStore` under a generated key placed on `Character.ConnectSecretKey`. The plaintext connect string MUST NEVER be written into any domain text field (`Character.Name`, `World.*`, etc.).
- Commits (exact prefixes): `feat(core):` (Task 1), `feat(ui):` (Task 2), `feat(web):` (Task 3).

## Reference Facts (verified against the codebase — do not re-derive)

**Domain** (`SharpClient.Core.Domain`, all `sealed`, all props settable):
```csharp
World    { Guid Id; string Name; string Host; int Port; List<Character> Characters; List<TriggerRule> Triggers; List<AliasRule> Aliases; }
Character{ Guid Id; Guid WorldId; string Name; string? ConnectSecretKey; List<TriggerRule> Triggers; List<AliasRule> Aliases; }
```
`Id` defaults to `Guid.NewGuid()`; collections default to `[]`.

**Persistence** (`SharpClient.Core.Persistence`):
```csharp
public interface IWorldStore {
    Task<IReadOnlyList<World>> GetWorldsAsync(CancellationToken cancellationToken = default);
    Task AddWorldAsync(World world, CancellationToken cancellationToken = default);
    Task UpdateWorldAsync(World world, CancellationToken cancellationToken = default);
    Task DeleteWorldAsync(Guid worldId, CancellationToken cancellationToken = default);
}
public interface ISecretStore {
    Task SetAsync(string key, string secret);
    Task<string?> GetAsync(string key);
    Task RemoveAsync(string key);
}
```

**Sessions** (`SharpClient.Core.Sessions`):
```csharp
public interface ISession : IAsyncDisposable {
    IReadOnlyList<ScrollbackLine> Scrollback { get; }
    event Action<ScrollbackLine>? LineAppended;
    event Action<ConnectionState>? StateChanged;
    ConnectionState State { get; }
    string CharacterName { get; }
    string WorldName { get; }
    Task ConnectAsync(string host, int port, CancellationToken cancellationToken = default);
    Task SendAsync(string line);
}
public interface ISessionManager {
    IReadOnlyList<ISession> Sessions { get; }
    ISession? Active { get; }
    event Action? Changed;
    void Add(ISession session);          // auto-activates the added session
    void Activate(ISession session);
    Task CloseAsync(ISession session);
}
```
`SessionManager : ISessionManager` is registered in `SharpClient.Web/Program.cs` as a **singleton**; `SessionsViewModel` is also a singleton there. The Home page (`/`) renders `<SessionScreen Vm="@Vm" />`.

**EF** (`SharpClient.Data`): `AppDbContext(IAppStorage storage)` builds SQLite options from `storage.GetDatabasePath()`. `WorldStore(AppDbContext db) : IWorldStore` (delete-then-reinsert on update; lazy `EnsureCreatedAsync`). `IAppStorage` lives in `SharpClient.Core.Platform` and exposes `string GetDatabasePath()`.

**Existing test fakes:** `tests/SharpClient.Tests/Fakes/FakeSecretStore.cs` (in-memory `ISecretStore`, namespace `SharpClient.Tests.Fakes`); `tests/SharpClient.Tests/Sessions/FakeSession.cs` (`ISession`, namespace `SharpClient.Tests.Sessions`, parameterless, tracks `Sent`/`Disposed`, settable `CharacterName`/`WorldName`); `tests/SharpClient.UI.Tests/UiFakeSession.cs` (`ISession`, namespace `SharpClient.UI.Tests`).

**Web demo:** `src/SharpClient.Web/DemoSession.cs` — `sealed class DemoSession : ISession` with settable `CharacterName`/`WorldName`/`State`, `AppendLine(string ansiLine)`, no-op `StateChanged`.

**Component patterns:** `SharpClient.UI/_Imports.razor` only imports `Microsoft.AspNetCore.Components.Web` — components must `@using SharpClient.Core.*` themselves. IDisposable subscribe/unsubscribe pattern is in `SessionScreen.razor` (`OnInitialized` adds `Vm.Changed += OnVmChanged`, `Dispose` removes it, `OnVmChanged() => InvokeAsync(StateHasChanged)`). `[Parameter]` defaults use `= null!;`.

---

## File Structure

**Create:**
- `src/SharpClient.Core/Sessions/ISessionLauncher.cs` — Connect abstraction (Task 1).
- `src/SharpClient.Core/Presentation/WorldManagerViewModel.cs` — the VM (Task 1).
- `tests/SharpClient.Tests/Fakes/FakeWorldStore.cs` — in-memory `IWorldStore` (Task 1).
- `tests/SharpClient.Tests/Fakes/FakeSessionLauncher.cs` — recording `ISessionLauncher` (Task 1).
- `tests/SharpClient.Tests/Presentation/WorldManagerViewModelTests.cs` — VM tests (Task 1).
- `src/SharpClient.UI/Components/WorldEditor.razor` — Add/Edit World modal (Task 2).
- `src/SharpClient.UI/Components/CharacterEditor.razor` — Add/Edit Character modal (Task 2).
- `src/SharpClient.UI/Components/WorldManager.razor` — main screen (Task 2).
- `tests/SharpClient.UI.Tests/UiFakeWorldStore.cs` — in-memory `IWorldStore` for UI tests (Task 2).
- `tests/SharpClient.UI.Tests/UiFakeSecretStore.cs` — in-memory `ISecretStore` for UI tests (Task 2).
- `tests/SharpClient.UI.Tests/UiFakeSessionLauncher.cs` — `ISessionLauncher` for UI tests (Task 2).
- `tests/SharpClient.UI.Tests/WorldManagerTests.cs` — component tests (Task 2).
- `src/SharpClient.Web/WebAppStorage.cs` — `IAppStorage` impl (Task 3).
- `src/SharpClient.Web/WebSecretStore.cs` — in-memory `ISecretStore` (Task 3).
- `src/SharpClient.Web/DemoSessionLauncher.cs` — `ISessionLauncher` returning `DemoSession` (Task 3).
- `src/SharpClient.Web/Components/Pages/Worlds.razor` — `/worlds` page (Task 3).

**Modify:**
- `src/SharpClient.Web/Program.cs` — register the new services (Task 3).
- `src/SharpClient.Web/Components/Layout/MainLayout.razor` — add nav between Home and Worlds (Task 3).
- `src/SharpClient.Web/wwwroot/app.css` — world-manager + nav styles (Task 3).

---

## Task 1: Core — `ISessionLauncher` + `WorldManagerViewModel` (+ VM tests)

**Files:**
- Create: `src/SharpClient.Core/Sessions/ISessionLauncher.cs`
- Create: `src/SharpClient.Core/Presentation/WorldManagerViewModel.cs`
- Create: `tests/SharpClient.Tests/Fakes/FakeWorldStore.cs`
- Create: `tests/SharpClient.Tests/Fakes/FakeSessionLauncher.cs`
- Test: `tests/SharpClient.Tests/Presentation/WorldManagerViewModelTests.cs`

**Interfaces:**
- Consumes: `IWorldStore`, `ISecretStore` (`SharpClient.Core.Persistence`); `ISession`, `ISessionManager` (`SharpClient.Core.Sessions`); `World`, `Character` (`SharpClient.Core.Domain`). See Reference Facts for exact signatures.
- Produces (later tasks rely on these EXACT names/types):
  - `interface ISessionLauncher { Task<ISession> LaunchAsync(World world, Character character, CancellationToken cancellationToken = default); }` in `SharpClient.Core.Sessions`.
  - `sealed class WorldManagerViewModel` in `SharpClient.Core.Presentation` with ctor `(IWorldStore store, ISecretStore secrets, ISessionManager sessions, ISessionLauncher launcher)` and members:
    - `IReadOnlyList<World> Worlds { get; }`
    - `bool HasWorlds { get; }`
    - `event Action? Changed;`
    - `Task LoadAsync(CancellationToken cancellationToken = default)`
    - `Task AddWorldAsync(string name, string host, int port, CancellationToken cancellationToken = default)`
    - `Task UpdateWorldAsync(World world, CancellationToken cancellationToken = default)`
    - `Task DeleteWorldAsync(Guid worldId, CancellationToken cancellationToken = default)`
    - `Task AddCharacterAsync(World world, string name, string? connectString, CancellationToken cancellationToken = default)`
    - `Task UpdateCharacterAsync(World world, Character character, string name, string? connectString, CancellationToken cancellationToken = default)`
    - `Task DeleteCharacterAsync(World world, Character character, CancellationToken cancellationToken = default)`
    - `Task ConnectAsync(World world, Character character, CancellationToken cancellationToken = default)`
  - Secret key scheme: `$"connect:{character.Id:N}"` (stable per character id).

- [ ] **Step 1: Create `ISessionLauncher`**

Create `src/SharpClient.Core/Sessions/ISessionLauncher.cs`:

```csharp
using SharpClient.Core.Domain;

namespace SharpClient.Core.Sessions;

/// <summary>
/// Creates a started <see cref="ISession"/> for a character on a world. Implementations
/// own the transport choice (real telnet vs. a web/demo stand-in) so the Connect flow
/// never hard-depends on sockets.
/// </summary>
/// <remarks>
/// Implementations are ALSO responsible for auto-sending the character's connect string
/// after connecting: resolve <see cref="Character.ConnectSecretKey"/> via
/// <c>ISecretStore</c> and send the resolved value on connect. The returned session is
/// already started; its identity is the character/world names.
/// </remarks>
public interface ISessionLauncher
{
    Task<ISession> LaunchAsync(World world, Character character, CancellationToken cancellationToken = default);
}
```

- [ ] **Step 2: Create the VM skeleton (compiles, methods throw)**

Create `src/SharpClient.Core/Presentation/WorldManagerViewModel.cs`. Start with `NotImplementedException` bodies so the project compiles and tests can reference real signatures before TDD fills them in:

```csharp
using SharpClient.Core.Domain;
using SharpClient.Core.Persistence;
using SharpClient.Core.Sessions;

namespace SharpClient.Core.Presentation;

public sealed class WorldManagerViewModel
{
    private readonly IWorldStore _store;
    private readonly ISecretStore _secrets;
    private readonly ISessionManager _sessions;
    private readonly ISessionLauncher _launcher;

    private IReadOnlyList<World> _worlds = [];

    public WorldManagerViewModel(
        IWorldStore store,
        ISecretStore secrets,
        ISessionManager sessions,
        ISessionLauncher launcher)
    {
        _store = store;
        _secrets = secrets;
        _sessions = sessions;
        _launcher = launcher;
    }

    public IReadOnlyList<World> Worlds => _worlds;

    public bool HasWorlds => _worlds.Count > 0;

    public event Action? Changed;

    public Task LoadAsync(CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task AddWorldAsync(string name, string host, int port, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task UpdateWorldAsync(World world, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task DeleteWorldAsync(Guid worldId, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task AddCharacterAsync(World world, string name, string? connectString, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task UpdateCharacterAsync(World world, Character character, string name, string? connectString, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task DeleteCharacterAsync(World world, Character character, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task ConnectAsync(World world, Character character, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();
}
```

- [ ] **Step 3: Build Core to confirm it compiles**

Run: `dotnet build src/SharpClient.Core/SharpClient.Core.csproj`
Expected: Build succeeded, 0 warnings, 0 errors.

- [ ] **Step 4: Create the test fakes**

Create `tests/SharpClient.Tests/Fakes/FakeWorldStore.cs`:

```csharp
using SharpClient.Core.Domain;
using SharpClient.Core.Persistence;

namespace SharpClient.Tests.Fakes;

public sealed class FakeWorldStore : IWorldStore
{
    private readonly List<World> _worlds = [];

    public int AddCount { get; private set; }
    public int UpdateCount { get; private set; }
    public int DeleteCount { get; private set; }

    public Task<IReadOnlyList<World>> GetWorldsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<World>>(_worlds.ToList());

    public Task AddWorldAsync(World world, CancellationToken cancellationToken = default)
    {
        AddCount++;
        _worlds.Add(world);
        return Task.CompletedTask;
    }

    public Task UpdateWorldAsync(World world, CancellationToken cancellationToken = default)
    {
        UpdateCount++;
        var index = _worlds.FindIndex(w => w.Id == world.Id);
        if (index >= 0)
        {
            _worlds[index] = world;
        }
        else
        {
            _worlds.Add(world);
        }
        return Task.CompletedTask;
    }

    public Task DeleteWorldAsync(Guid worldId, CancellationToken cancellationToken = default)
    {
        DeleteCount++;
        _worlds.RemoveAll(w => w.Id == worldId);
        return Task.CompletedTask;
    }
}
```

Create `tests/SharpClient.Tests/Fakes/FakeSessionLauncher.cs` (reuses the existing `FakeSession` in `SharpClient.Tests.Sessions`):

```csharp
using SharpClient.Core.Domain;
using SharpClient.Core.Sessions;
using SharpClient.Tests.Sessions;

namespace SharpClient.Tests.Fakes;

public sealed class FakeSessionLauncher : ISessionLauncher
{
    public int LaunchCount { get; private set; }
    public World? LastWorld { get; private set; }
    public Character? LastCharacter { get; private set; }
    public FakeSession Session { get; } = new();

    public Task<ISession> LaunchAsync(World world, Character character, CancellationToken cancellationToken = default)
    {
        LaunchCount++;
        LastWorld = world;
        LastCharacter = character;
        return Task.FromResult<ISession>(Session);
    }
}
```

- [ ] **Step 5: Write the failing VM tests**

Create `tests/SharpClient.Tests/Presentation/WorldManagerViewModelTests.cs`. These tests reference `SessionManager` (real, from Core), `FakeSecretStore`, `FakeWorldStore`, `FakeSessionLauncher`.

```csharp
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
}
```

- [ ] **Step 6: Run the new tests to verify they FAIL**

Run: `dotnet run --project tests/SharpClient.Tests/SharpClient.Tests.csproj --filter "*WorldManagerViewModel*"`
Expected: tests FAIL (the VM methods throw `NotImplementedException`).
(If the `--filter` syntax is unavailable in this TUnit version, run the whole suite — the new tests still fail.)

- [ ] **Step 7: Implement the VM**

Replace the body of `src/SharpClient.Core/Presentation/WorldManagerViewModel.cs` (keep the ctor, fields, `Worlds`, `HasWorlds`, `Changed` from Step 2; replace the method stubs with real implementations and add the helpers):

```csharp
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        _worlds = await _store.GetWorldsAsync(cancellationToken);
        RaiseChanged();
    }

    public async Task AddWorldAsync(string name, string host, int port, CancellationToken cancellationToken = default)
    {
        var world = new World { Name = name, Host = host, Port = port };
        await _store.AddWorldAsync(world, cancellationToken);
        await LoadAsync(cancellationToken);
    }

    public async Task UpdateWorldAsync(World world, CancellationToken cancellationToken = default)
    {
        await _store.UpdateWorldAsync(world, cancellationToken);
        await LoadAsync(cancellationToken);
    }

    public async Task DeleteWorldAsync(Guid worldId, CancellationToken cancellationToken = default)
    {
        await _store.DeleteWorldAsync(worldId, cancellationToken);
        await LoadAsync(cancellationToken);
    }

    public async Task AddCharacterAsync(World world, string name, string? connectString, CancellationToken cancellationToken = default)
    {
        var character = new Character { WorldId = world.Id, Name = name };
        await ApplyConnectSecretAsync(character, connectString);
        world.Characters.Add(character);
        await _store.UpdateWorldAsync(world, cancellationToken);
        await LoadAsync(cancellationToken);
    }

    public async Task UpdateCharacterAsync(World world, Character character, string name, string? connectString, CancellationToken cancellationToken = default)
    {
        character.Name = name;
        await ApplyConnectSecretAsync(character, connectString);
        await _store.UpdateWorldAsync(world, cancellationToken);
        await LoadAsync(cancellationToken);
    }

    public async Task DeleteCharacterAsync(World world, Character character, CancellationToken cancellationToken = default)
    {
        world.Characters.RemoveAll(c => c.Id == character.Id);
        if (character.ConnectSecretKey is { } key)
        {
            await _secrets.RemoveAsync(key);
        }
        await _store.UpdateWorldAsync(world, cancellationToken);
        await LoadAsync(cancellationToken);
    }

    public async Task ConnectAsync(World world, Character character, CancellationToken cancellationToken = default)
    {
        var session = await _launcher.LaunchAsync(world, character, cancellationToken);
        _sessions.Add(session);
    }

    // Stores the connect string as a secret keyed by the character id; never writes it
    // into a domain text field. A blank connect string leaves any existing secret in
    // place (edit cannot clear a secret — functional over fancy).
    private async Task ApplyConnectSecretAsync(Character character, string? connectString)
    {
        if (string.IsNullOrWhiteSpace(connectString))
        {
            return;
        }

        var key = character.ConnectSecretKey ?? $"connect:{character.Id:N}";
        character.ConnectSecretKey = key;
        await _secrets.SetAsync(key, connectString);
    }

    private void RaiseChanged() => Changed?.Invoke();
```

- [ ] **Step 8: Run the new tests to verify they PASS**

Run: `dotnet run --project tests/SharpClient.Tests/SharpClient.Tests.csproj`
Expected: ALL tests PASS — 75 pre-existing + 8 new = 83 passing, 0 warnings.

- [ ] **Step 9: Commit**

```bash
git add src/SharpClient.Core/Sessions/ISessionLauncher.cs \
        src/SharpClient.Core/Presentation/WorldManagerViewModel.cs \
        tests/SharpClient.Tests/Fakes/FakeWorldStore.cs \
        tests/SharpClient.Tests/Fakes/FakeSessionLauncher.cs \
        tests/SharpClient.Tests/Presentation/WorldManagerViewModelTests.cs
git commit -m "feat(core): add ISessionLauncher and WorldManagerViewModel

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01HHCRc5CJ6595iMzEgYYdQp"
```

---

## Task 2: UI — `WorldEditor`, `CharacterEditor`, `WorldManager` components (+ bUnit tests)

**Files:**
- Create: `src/SharpClient.UI/Components/WorldEditor.razor`
- Create: `src/SharpClient.UI/Components/CharacterEditor.razor`
- Create: `src/SharpClient.UI/Components/WorldManager.razor`
- Create: `tests/SharpClient.UI.Tests/UiFakeWorldStore.cs`
- Create: `tests/SharpClient.UI.Tests/UiFakeSecretStore.cs`
- Create: `tests/SharpClient.UI.Tests/UiFakeSessionLauncher.cs`
- Test: `tests/SharpClient.UI.Tests/WorldManagerTests.cs`

**Interfaces:**
- Consumes: `WorldManagerViewModel` (Task 1) and its members; `World`, `Character` (`SharpClient.Core.Domain`); `ISessionLauncher`, `ISession`, `SessionManager` (Core); `IWorldStore`, `ISecretStore` (Core).
- Produces (Task 3 / web host relies on these):
  - `WorldManager` component with `[Parameter] public WorldManagerViewModel Vm { get; set; } = null!;` — renders the full screen; expects the caller to have called `Vm.LoadAsync()` (the page does this in `OnInitializedAsync`).
  - `WorldEditor` and `CharacterEditor` are internal to `WorldManager` (used as child components); not referenced directly by the web host.
- CSS classes consumed (defined in Task 3's `app.css`): `sc-wm`, `sc-wm-header`, `sc-wm-titles`, `sc-wm-title`, `sc-wm-sub`, `sc-wm-addbtn`, `sc-wm-empty`, `sc-wm-empty-title`, `sc-wm-empty-text`, `sc-wm-empty-cta`, `sc-world`, `sc-world-row`, `sc-world-badge`, `sc-world-main`, `sc-world-name`, `sc-world-addr`, `sc-world-meta`, `sc-world-charcount`, `sc-chev`, `sc-chev-open`, `sc-char-row`, `sc-char-badge`, `sc-char-name`, `sc-connect-btn`, `sc-icon-btn`, `sc-icon-btn-danger`, `sc-world-actions`, `sc-addchar`, `sc-delete-world`, `sc-no-chars`, `sc-modal-overlay`, `sc-modal`, `sc-modal-header`, `sc-modal-title`, `sc-modal-sub`, `sc-modal-close`, `sc-field`, `sc-field-label`, `sc-field-input`, `sc-field-input-secret`, `sc-modal-submit`.

- [ ] **Step 1: Create `WorldEditor.razor`**

Create `src/SharpClient.UI/Components/WorldEditor.razor`. Add/Edit World modal. `World` parameter null ⇒ Add mode; non-null ⇒ Edit mode.

```razor
@using SharpClient.Core.Domain
@using SharpClient.Core.Presentation

<div class="sc-modal-overlay" @onclick="OnClose">
    <div class="sc-modal" @onclick:stopPropagation="true">
        <div class="sc-modal-header">
            <div>
                <div class="sc-modal-title">@(_isEdit ? "Edit World" : "Add World")</div>
                <div class="sc-modal-sub">a MUSH / MUD server</div>
            </div>
            <button type="button" class="sc-modal-close" @onclick="OnClose">×</button>
        </div>

        <label class="sc-field">
            <span class="sc-field-label">world name</span>
            <input class="sc-field-input" @bind="_name" @bind:event="oninput" placeholder="e.g. Sindome" />
        </label>
        <label class="sc-field">
            <span class="sc-field-label">host</span>
            <input class="sc-field-input" @bind="_host" @bind:event="oninput" placeholder="e.g. sindome.org" />
        </label>
        <label class="sc-field">
            <span class="sc-field-label">port</span>
            <input class="sc-field-input" type="number" @bind="_port" @bind:event="oninput" placeholder="23" />
        </label>

        <button type="button" class="sc-modal-submit" disabled="@(!CanSubmit)" @onclick="SubmitAsync">
            @(_isEdit ? "Save world" : "Add world")
        </button>
    </div>
</div>

@code {
    [Parameter] public WorldManagerViewModel Vm { get; set; } = null!;
    [Parameter] public World? World { get; set; }
    [Parameter] public EventCallback OnClose { get; set; }

    private bool _isEdit;
    private string _name = string.Empty;
    private string _host = string.Empty;
    private int _port = 23;

    private bool CanSubmit => !string.IsNullOrWhiteSpace(_name) && !string.IsNullOrWhiteSpace(_host);

    protected override void OnParametersSet()
    {
        _isEdit = World is not null;
        if (World is not null)
        {
            _name = World.Name;
            _host = World.Host;
            _port = World.Port;
        }
    }

    private async Task SubmitAsync()
    {
        if (!CanSubmit)
        {
            return;
        }

        if (World is null)
        {
            await Vm.AddWorldAsync(_name.Trim(), _host.Trim(), _port);
        }
        else
        {
            World.Name = _name.Trim();
            World.Host = _host.Trim();
            World.Port = _port;
            await Vm.UpdateWorldAsync(World);
        }

        await OnClose.InvokeAsync();
    }
}
```

- [ ] **Step 2: Create `CharacterEditor.razor`**

Create `src/SharpClient.UI/Components/CharacterEditor.razor`. The connect string is a secret; on Edit it renders blank with a "leave blank to keep" hint (the VM keeps the existing secret when blank).

```razor
@using SharpClient.Core.Domain
@using SharpClient.Core.Presentation

<div class="sc-modal-overlay" @onclick="OnClose">
    <div class="sc-modal" @onclick:stopPropagation="true">
        <div class="sc-modal-header">
            <div>
                <div class="sc-modal-title">@(_isEdit ? "Edit Character" : "Add Character")</div>
                <div class="sc-modal-sub">on @World.Name</div>
            </div>
            <button type="button" class="sc-modal-close" @onclick="OnClose">×</button>
        </div>

        <label class="sc-field">
            <span class="sc-field-label">character name</span>
            <input class="sc-field-input" @bind="_name" @bind:event="oninput" placeholder="e.g. Vesper" />
        </label>
        <label class="sc-field">
            <span class="sc-field-label">connect string · auto-sent on connect</span>
            <input class="sc-field-input sc-field-input-secret" @bind="_connect" @bind:event="oninput"
                   placeholder="@(_isEdit ? "leave blank to keep current" : "connect Vesper yourpassword")" />
        </label>

        <button type="button" class="sc-modal-submit" disabled="@(!CanSubmit)" @onclick="SubmitAsync">
            @(_isEdit ? "Save character" : "Add character")
        </button>
    </div>
</div>

@code {
    [Parameter] public WorldManagerViewModel Vm { get; set; } = null!;
    [Parameter] public World World { get; set; } = null!;
    [Parameter] public Character? Character { get; set; }
    [Parameter] public EventCallback OnClose { get; set; }

    private bool _isEdit;
    private string _name = string.Empty;
    private string _connect = string.Empty;

    private bool CanSubmit => !string.IsNullOrWhiteSpace(_name);

    protected override void OnParametersSet()
    {
        _isEdit = Character is not null;
        if (Character is not null)
        {
            _name = Character.Name;
            _connect = string.Empty; // secret never echoed back
        }
    }

    private async Task SubmitAsync()
    {
        if (!CanSubmit)
        {
            return;
        }

        var connect = string.IsNullOrWhiteSpace(_connect) ? null : _connect.Trim();

        if (Character is null)
        {
            await Vm.AddCharacterAsync(World, _name.Trim(), connect);
        }
        else
        {
            await Vm.UpdateCharacterAsync(World, Character, _name.Trim(), connect);
        }

        await OnClose.InvokeAsync();
    }
}
```

- [ ] **Step 3: Create `WorldManager.razor`**

Create `src/SharpClient.UI/Components/WorldManager.razor`. Worlds list, expandable characters, Connect/edit/delete, empty state, and the two modals controlled by local state. Expansion is keyed by `Guid` so it survives the post-mutation reload.

```razor
@using SharpClient.Core.Domain
@using SharpClient.Core.Presentation
@implements IDisposable

<div class="sc-wm">
    <div class="sc-wm-header">
        <div class="sc-wm-titles">
            <div class="sc-wm-title">Worlds</div>
            <div class="sc-wm-sub">@WorldCountLabel</div>
        </div>
        <button type="button" class="sc-wm-addbtn" @onclick="OpenAddWorld">+ World</button>
    </div>

    @if (!Vm.HasWorlds)
    {
        <div class="sc-wm-empty">
            <div class="sc-wm-empty-title">No worlds yet</div>
            <div class="sc-wm-empty-text">Add a MUD or MUSH server to connect your first character.</div>
            <button type="button" class="sc-wm-empty-cta" @onclick="OpenAddWorld">+ Add your first world</button>
        </div>
    }
    else
    {
        @foreach (var world in Vm.Worlds)
        {
            var isOpen = _expanded.Contains(world.Id);
            <div class="sc-world">
                <div class="sc-world-row" @onclick="() => ToggleExpand(world.Id)">
                    <div class="sc-world-badge">@Initial(world.Name)</div>
                    <div class="sc-world-main">
                        <div class="sc-world-name">@world.Name</div>
                        <div class="sc-world-addr">@world.Host:@world.Port</div>
                    </div>
                    <div class="sc-world-meta">
                        <span class="sc-world-charcount">@CharLabel(world)</span>
                        <span class="sc-chev @(isOpen ? "sc-chev-open" : "")">⌄</span>
                    </div>
                </div>

                @if (isOpen)
                {
                    @if (world.Characters.Count == 0)
                    {
                        <div class="sc-no-chars">no characters yet</div>
                    }
                    else
                    {
                        @foreach (var character in world.Characters)
                        {
                            <div class="sc-char-row">
                                <div class="sc-char-badge">@Initial(character.Name)</div>
                                <div class="sc-char-name">@character.Name</div>
                                <button type="button" class="sc-connect-btn" @onclick="() => ConnectAsync(world, character)">Connect</button>
                                <button type="button" class="sc-icon-btn" title="Edit character" @onclick="() => OpenEditCharacter(world, character)">✎</button>
                                <button type="button" class="sc-icon-btn sc-icon-btn-danger" title="Delete character" @onclick="() => DeleteCharacterAsync(world, character)">🗑</button>
                            </div>
                        }
                    }

                    <div class="sc-world-actions">
                        <button type="button" class="sc-addchar" @onclick="() => OpenAddCharacter(world)">+ add character</button>
                        <button type="button" class="sc-delete-world" @onclick="() => DeleteWorldAsync(world)">delete</button>
                    </div>
                }
            </div>
        }
    }
</div>

@if (_worldEditorOpen)
{
    <WorldEditor Vm="Vm" World="_editingWorld" OnClose="CloseWorldEditor" />
}
@if (_charEditorOpen && _charWorld is not null)
{
    <CharacterEditor Vm="Vm" World="_charWorld" Character="_editingCharacter" OnClose="CloseCharacterEditor" />
}

@code {
    [Parameter] public WorldManagerViewModel Vm { get; set; } = null!;

    private readonly HashSet<Guid> _expanded = [];

    private bool _worldEditorOpen;
    private World? _editingWorld;

    private bool _charEditorOpen;
    private World? _charWorld;
    private Character? _editingCharacter;

    protected override void OnInitialized() => Vm.Changed += OnVmChanged;

    private void OnVmChanged() => InvokeAsync(StateHasChanged);

    private string WorldCountLabel
    {
        get
        {
            var worlds = Vm.Worlds.Count;
            var chars = Vm.Worlds.Sum(w => w.Characters.Count);
            return $"{worlds} world{(worlds == 1 ? "" : "s")} · {chars} character{(chars == 1 ? "" : "s")}";
        }
    }

    private static string Initial(string name) =>
        string.IsNullOrEmpty(name) ? "?" : name[..1].ToUpperInvariant();

    private static string CharLabel(World world)
    {
        var count = world.Characters.Count;
        return $"{count} char{(count == 1 ? "" : "s")}";
    }

    private void ToggleExpand(Guid worldId)
    {
        if (!_expanded.Remove(worldId))
        {
            _expanded.Add(worldId);
        }
    }

    private void OpenAddWorld()
    {
        _editingWorld = null;
        _worldEditorOpen = true;
    }

    private void CloseWorldEditor()
    {
        _worldEditorOpen = false;
        _editingWorld = null;
    }

    private void OpenAddCharacter(World world)
    {
        _charWorld = world;
        _editingCharacter = null;
        _charEditorOpen = true;
    }

    private void OpenEditCharacter(World world, Character character)
    {
        _charWorld = world;
        _editingCharacter = character;
        _charEditorOpen = true;
    }

    private void CloseCharacterEditor()
    {
        _charEditorOpen = false;
        _charWorld = null;
        _editingCharacter = null;
    }

    private async Task DeleteWorldAsync(World world) => await Vm.DeleteWorldAsync(world.Id);

    private async Task DeleteCharacterAsync(World world, Character character) =>
        await Vm.DeleteCharacterAsync(world, character);

    private async Task ConnectAsync(World world, Character character) =>
        await Vm.ConnectAsync(world, character);

    public void Dispose() => Vm.Changed -= OnVmChanged;
}
```

- [ ] **Step 4: Build the UI project**

Run: `dotnet build src/SharpClient.UI/SharpClient.UI.csproj`
Expected: Build succeeded, 0 warnings, 0 errors.

- [ ] **Step 5: Create UI-test fakes**

Create `tests/SharpClient.UI.Tests/UiFakeWorldStore.cs`:

```csharp
using SharpClient.Core.Domain;
using SharpClient.Core.Persistence;

namespace SharpClient.UI.Tests;

public sealed class UiFakeWorldStore : IWorldStore
{
    private readonly List<World> _worlds = [];

    public Task<IReadOnlyList<World>> GetWorldsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<World>>(_worlds.ToList());

    public Task AddWorldAsync(World world, CancellationToken cancellationToken = default)
    {
        _worlds.Add(world);
        return Task.CompletedTask;
    }

    public Task UpdateWorldAsync(World world, CancellationToken cancellationToken = default)
    {
        var index = _worlds.FindIndex(w => w.Id == world.Id);
        if (index >= 0)
        {
            _worlds[index] = world;
        }
        else
        {
            _worlds.Add(world);
        }
        return Task.CompletedTask;
    }

    public Task DeleteWorldAsync(Guid worldId, CancellationToken cancellationToken = default)
    {
        _worlds.RemoveAll(w => w.Id == worldId);
        return Task.CompletedTask;
    }
}
```

Create `tests/SharpClient.UI.Tests/UiFakeSecretStore.cs`:

```csharp
using SharpClient.Core.Persistence;

namespace SharpClient.UI.Tests;

public sealed class UiFakeSecretStore : ISecretStore
{
    private readonly Dictionary<string, string> _store = new();

    public Task SetAsync(string key, string secret)
    {
        _store[key] = secret;
        return Task.CompletedTask;
    }

    public Task<string?> GetAsync(string key) =>
        Task.FromResult(_store.TryGetValue(key, out var value) ? value : null);

    public Task RemoveAsync(string key)
    {
        _store.Remove(key);
        return Task.CompletedTask;
    }
}
```

Create `tests/SharpClient.UI.Tests/UiFakeSessionLauncher.cs` (reuses the existing `UiFakeSession`):

```csharp
using SharpClient.Core.Domain;
using SharpClient.Core.Sessions;

namespace SharpClient.UI.Tests;

public sealed class UiFakeSessionLauncher : ISessionLauncher
{
    public int LaunchCount { get; private set; }

    public Task<ISession> LaunchAsync(World world, Character character, CancellationToken cancellationToken = default)
    {
        LaunchCount++;
        return Task.FromResult<ISession>(new UiFakeSession
        {
            CharacterName = character.Name,
            WorldName = world.Name,
        });
    }
}
```

- [ ] **Step 6: Write the failing component tests**

Create `tests/SharpClient.UI.Tests/WorldManagerTests.cs`. bUnit 2.7.2 API: `new BunitContext()` and `ctx.Render<T>(...)` (NOT `TestContext`/`RenderComponent` — those are bUnit v1). Mirror the existing style in `tests/SharpClient.UI.Tests/SessionTabsTests.cs` (verified: `using Bunit;`, `using var ctx = new BunitContext();`, `ctx.Render<T>(p => p.Add(c => c.Vm, vm))`).

```csharp
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
    public async Task ExpandingWorldShowsCharacterAndConnectButton()
    {
        var vm = await BuildSeededVmAsync();
        using var ctx = new BunitContext();
        var cut = ctx.Render<WorldManager>(p => p.Add(c => c.Vm, vm));

        cut.Find(".sc-world-row").Click();

        await Assert.That(cut.Markup).Contains("Vesper");
        await Assert.That(cut.FindAll(".sc-connect-btn")).IsNotEmpty();
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
```

- [ ] **Step 7: Run the new component tests to verify they FAIL (then PASS)**

Run: `dotnet run --project tests/SharpClient.UI.Tests/SharpClient.UI.Tests.csproj`
Expected initially: the 3 new tests may fail to compile/find until Steps 1-5 are present. Since the components and fakes are already created above, this run should COMPILE and PASS all tests — 8 pre-existing + 3 new = 11 passing, 0 warnings.
(If a test fails, fix the component markup/class names to match the assertions — e.g. ensure `.sc-world-row`, `.sc-connect-btn`, `.sc-wm-empty-cta` exist and the empty state text is exactly "No worlds yet".)

- [ ] **Step 8: Commit**

```bash
git add src/SharpClient.UI/Components/WorldEditor.razor \
        src/SharpClient.UI/Components/CharacterEditor.razor \
        src/SharpClient.UI/Components/WorldManager.razor \
        tests/SharpClient.UI.Tests/UiFakeWorldStore.cs \
        tests/SharpClient.UI.Tests/UiFakeSecretStore.cs \
        tests/SharpClient.UI.Tests/UiFakeSessionLauncher.cs \
        tests/SharpClient.UI.Tests/WorldManagerTests.cs
git commit -m "feat(ui): add WorldManager, WorldEditor, CharacterEditor components

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01HHCRc5CJ6595iMzEgYYdQp"
```

---

## Task 3: Web — host wiring, `/worlds` page, nav, styles

**Files:**
- Create: `src/SharpClient.Web/WebAppStorage.cs`
- Create: `src/SharpClient.Web/WebSecretStore.cs`
- Create: `src/SharpClient.Web/DemoSessionLauncher.cs`
- Create: `src/SharpClient.Web/Components/Pages/Worlds.razor`
- Modify: `src/SharpClient.Web/Program.cs`
- Modify: `src/SharpClient.Web/Components/Layout/MainLayout.razor`
- Modify: `src/SharpClient.Web/wwwroot/app.css`

**Interfaces:**
- Consumes: `WorldManagerViewModel`, `ISessionLauncher` (Core, Task 1); `WorldManager` (UI, Task 2); `IWorldStore`, `ISecretStore`, `IAppStorage` (Core); `WorldStore`, `AppDbContext` (`SharpClient.Data`); `DemoSession`, `SessionManager` (existing in Web).
- Produces: a working `/worlds` route + DI so Connect opens a session visible on `/`.
- NOTE: `SharpClient.Web.csproj` references Core + UI but NOT `SharpClient.Data`. Add a `<ProjectReference>` to `SharpClient.Data` (Step 1) so `WorldStore`/`AppDbContext` are usable.

- [ ] **Step 1: Add the Data project reference to the web host**

Run:
```bash
dotnet add src/SharpClient.Web/SharpClient.Web.csproj reference src/SharpClient.Data/SharpClient.Data.csproj
```
Expected: "Reference ... added to the project."

- [ ] **Step 2: Create `WebAppStorage`**

Create `src/SharpClient.Web/WebAppStorage.cs`. Places the SQLite DB under the host's content root in an `App_Data` folder and ensures the directory exists.

```csharp
using SharpClient.Core.Platform;

namespace SharpClient.Web;

public sealed class WebAppStorage : IAppStorage
{
    private readonly string _path;

    public WebAppStorage(IWebHostEnvironment environment)
    {
        var dir = Path.Combine(environment.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "sharpclient.db");
    }

    public string GetDatabasePath() => _path;
}
```

- [ ] **Step 3: Create `WebSecretStore`**

Create `src/SharpClient.Web/WebSecretStore.cs`. In-memory (process-lifetime) secret store; thread-safe for the singleton lifetime.

```csharp
using System.Collections.Concurrent;
using SharpClient.Core.Persistence;

namespace SharpClient.Web;

public sealed class WebSecretStore : ISecretStore
{
    private readonly ConcurrentDictionary<string, string> _store = new();

    public Task SetAsync(string key, string secret)
    {
        _store[key] = secret;
        return Task.CompletedTask;
    }

    public Task<string?> GetAsync(string key) =>
        Task.FromResult(_store.TryGetValue(key, out var value) ? value : null);

    public Task RemoveAsync(string key)
    {
        _store.TryRemove(key, out _);
        return Task.CompletedTask;
    }
}
```

- [ ] **Step 4: Create `DemoSessionLauncher`**

Create `src/SharpClient.Web/DemoSessionLauncher.cs`. Returns a `DemoSession` seeded with a short connected feed; demonstrates the connect-string responsibility by resolving the secret and appending a masked confirmation line.

```csharp
using SharpClient.Core.Domain;
using SharpClient.Core.Persistence;
using SharpClient.Core.Sessions;

using CoreSession = SharpClient.Core.Sessions.ISession;

namespace SharpClient.Web;

public sealed class DemoSessionLauncher : ISessionLauncher
{
    private readonly ISecretStore _secrets;

    public DemoSessionLauncher(ISecretStore secrets) => _secrets = secrets;

    public async Task<CoreSession> LaunchAsync(World world, Character character, CancellationToken cancellationToken = default)
    {
        var session = new DemoSession
        {
            CharacterName = character.Name,
            WorldName = world.Name,
            State = ConnectionState.Connected,
        };

        session.AppendLine($"[1m[32m✓ Connected to {world.Name}[0m ([90m{world.Host}:{world.Port}[0m)");

        if (character.ConnectSecretKey is { } key)
        {
            var connect = await _secrets.GetAsync(key);
            if (!string.IsNullOrWhiteSpace(connect))
            {
                // Auto-send the connect string on connect (masked in the demo feed).
                session.AppendLine("[90m» sent connect string[0m");
            }
        }

        session.AppendLine($"[90mWelcome, {character.Name}. Type [4mhelp[0m to begin.[0m");
        return session;
    }
}
```

- [ ] **Step 5: Register services in `Program.cs`**

In `src/SharpClient.Web/Program.cs`, after the existing `SessionsViewModel` registration (line ~15) and before `var app = builder.Build();`, add:

```csharp
// World Manager: EF persistence + in-memory secrets + demo launcher.
builder.Services.AddSingleton<SharpClient.Core.Platform.IAppStorage, WebAppStorage>();
builder.Services.AddSingleton<SharpClient.Core.Persistence.ISecretStore, WebSecretStore>();
builder.Services.AddScoped<SharpClient.Data.AppDbContext>();
builder.Services.AddScoped<SharpClient.Core.Persistence.IWorldStore, SharpClient.Data.WorldStore>();
builder.Services.AddScoped<ISessionLauncher, DemoSessionLauncher>();
builder.Services.AddScoped<WorldManagerViewModel>(sp => new WorldManagerViewModel(
    sp.GetRequiredService<SharpClient.Core.Persistence.IWorldStore>(),
    sp.GetRequiredService<SharpClient.Core.Persistence.ISecretStore>(),
    sp.GetRequiredService<ISessionManager>(),
    sp.GetRequiredService<ISessionLauncher>()));
```

(If you prefer, add `using SharpClient.Core.Persistence;`, `using SharpClient.Core.Platform;`, and `using SharpClient.Data;` at the top instead of fully-qualifying — but only add a `using` if every symbol from it is used, to avoid the unused-using warning. The fully-qualified form above is safe.)

Leave the existing demo-session seeding (Vesper/Thorne/Doran) in place — the seeded session screen still works; `/worlds` starts with an empty world list so the empty state is reachable.

- [ ] **Step 6: Create the `/worlds` page**

Create `src/SharpClient.Web/Components/Pages/Worlds.razor`:

```razor
@page "/worlds"
@rendermode InteractiveServer

@using SharpClient.Core.Presentation
@using SharpClient.UI.Components

@inject WorldManagerViewModel Vm

<PageTitle>SharpClient · Worlds</PageTitle>

<WorldManager Vm="@Vm" />

@code {
    protected override async Task OnInitializedAsync() => await Vm.LoadAsync();
}
```

- [ ] **Step 7: Add nav in `MainLayout.razor`**

Read `src/SharpClient.Web/Components/Layout/MainLayout.razor` first. Add a fixed nav (does not disturb the 100vh session screen flow) just inside the layout markup, above `@Body`:

```razor
<nav class="sc-nav">
    <a href="/" class="sc-nav-link">Session</a>
    <a href="/worlds" class="sc-nav-link">Worlds</a>
</nav>
```

Keep the existing `@Body` and error-UI markup unchanged.

- [ ] **Step 8: Add world-manager + nav styles to `app.css`**

Append to `src/SharpClient.Web/wwwroot/app.css` (uses the existing design-token CSS variables `--panel`, `--elev`, `--outbg`, `--tx`, `--dim`, `--faint`, `--bd`, `--bd2`, `--acc2`, `--acc-soft`, `--acc-line`, `--pho`):

```css
/* ── World Manager ─────────────────────────────────────────────────────── */
.sc-wm { max-width: 560px; margin: 0 auto; padding: 18px 14px 80px; display: flex; flex-direction: column; gap: 10px; }
.sc-wm-header { display: flex; align-items: center; justify-content: space-between; margin-bottom: 6px; }
.sc-wm-title { font-size: 19px; font-weight: 600; color: #eff3f7; letter-spacing: -.01em; }
.sc-wm-sub { font-family: var(--mono); font-size: 11px; color: var(--dim); margin-top: 3px; }
.sc-wm-addbtn { font-family: var(--mono); font-size: 12px; font-weight: 500; color: var(--acc2); background: var(--acc-soft); border: 1px solid var(--acc-line); border-radius: 9px; padding: 7px 11px; cursor: pointer; }

.sc-wm-empty { display: flex; flex-direction: column; align-items: center; gap: 8px; text-align: center; padding: 60px 20px; }
.sc-wm-empty-title { font-size: 17px; font-weight: 600; color: #e7ecf2; }
.sc-wm-empty-text { font-size: 13px; color: var(--dim); max-width: 230px; line-height: 1.5; }
.sc-wm-empty-cta { margin-top: 14px; font-size: 13px; font-weight: 600; color: #0a0c10; background: var(--acc2); border: none; border-radius: 10px; padding: 11px 18px; cursor: pointer; box-shadow: 0 0 22px rgba(155, 126, 212, .35); }

.sc-world { background: var(--panel); border: 1px solid var(--bd); border-radius: 12px; overflow: hidden; }
.sc-world-row { display: flex; align-items: center; gap: 12px; padding: 13px 14px; cursor: pointer; }
.sc-world-badge { flex: none; width: 30px; height: 30px; border-radius: 8px; background: var(--elev); border: 1px solid var(--bd2); display: flex; align-items: center; justify-content: center; font-family: var(--mono); font-size: 13px; color: var(--acc2); }
.sc-world-main { flex: 1; min-width: 0; }
.sc-world-name { font-weight: 600; font-size: 15px; color: #e7ecf2; }
.sc-world-addr { font-family: var(--mono); font-size: 11px; color: var(--dim); margin-top: 2px; }
.sc-world-meta { display: flex; align-items: center; gap: 10px; }
.sc-world-charcount { font-family: var(--mono); font-size: 11px; color: var(--faint); }
.sc-chev { color: var(--faint); transition: transform .15s ease; }
.sc-chev-open { transform: rotate(180deg); }

.sc-char-row { display: flex; align-items: center; gap: 11px; padding: 10px 14px; border-top: 1px solid var(--bd); }
.sc-char-badge { flex: none; width: 26px; height: 26px; border-radius: 7px; background: var(--outbg); border: 1px solid var(--bd2); display: flex; align-items: center; justify-content: center; font-family: var(--mono); font-size: 11px; color: var(--dim); }
.sc-char-name { flex: 1; min-width: 0; font-size: 14px; color: #e7ecf2; }
.sc-connect-btn { flex: none; font-size: 12.5px; font-weight: 600; color: #0a0c10; background: var(--acc2); border: none; border-radius: 8px; padding: 8px 14px; cursor: pointer; }
.sc-icon-btn { flex: none; width: 30px; height: 30px; border-radius: 8px; background: transparent; border: 1px solid var(--bd); color: var(--faint); display: flex; align-items: center; justify-content: center; cursor: pointer; }
.sc-icon-btn-danger:hover { border-color: rgba(224, 108, 117, .5); color: #e06c75; }

.sc-no-chars { padding: 11px 10px; text-align: center; font-size: 12px; color: var(--faint); font-family: var(--mono); border-top: 1px solid var(--bd); }
.sc-world-actions { display: flex; gap: 8px; padding: 11px 14px; border-top: 1px solid var(--bd); }
.sc-addchar { flex: 1; display: flex; align-items: center; justify-content: center; gap: 8px; padding: 9px 10px; border-radius: 10px; border: 1px dashed var(--bd2); background: transparent; color: var(--dim); font-family: var(--mono); font-size: 12px; cursor: pointer; }
.sc-delete-world { flex: none; padding: 9px 12px; border-radius: 10px; border: 1px solid rgba(224, 108, 117, .28); background: rgba(224, 108, 117, .07); color: #e06c75; font-family: var(--mono); font-size: 12px; cursor: pointer; }

/* ── Modals ────────────────────────────────────────────────────────────── */
.sc-modal-overlay { position: fixed; inset: 0; background: rgba(4, 6, 9, .6); display: flex; align-items: center; justify-content: center; z-index: 50; padding: 16px; }
.sc-modal { width: 100%; max-width: 360px; background: var(--panel); border: 1px solid var(--bd2); border-radius: 14px; padding: 18px; display: flex; flex-direction: column; gap: 12px; }
.sc-modal-header { display: flex; align-items: flex-start; justify-content: space-between; }
.sc-modal-title { font-weight: 600; font-size: 14px; color: #e7ecf2; }
.sc-modal-sub { font-family: var(--mono); font-size: 10px; color: var(--dim); margin-top: 3px; }
.sc-modal-close { background: transparent; border: none; color: var(--dim); font-size: 18px; cursor: pointer; }
.sc-field { display: flex; flex-direction: column; gap: 6px; }
.sc-field-label { font-family: var(--mono); font-size: 11px; color: var(--dim); }
.sc-field-input { background: var(--outbg); border: 1px solid var(--bd2); border-radius: 9px; padding: 11px 12px; color: var(--tx); font-size: 14px; outline: none; }
.sc-field-input-secret { color: var(--acc2); font-family: var(--mono); font-size: 13px; }
.sc-modal-submit { margin-top: 4px; font-size: 14px; font-weight: 600; color: #0a0c10; background: var(--acc2); border: none; border-radius: 10px; padding: 13px; cursor: pointer; }
.sc-modal-submit:disabled { opacity: .5; cursor: not-allowed; }

/* ── Top nav ───────────────────────────────────────────────────────────── */
.sc-nav { position: fixed; top: 10px; right: 14px; z-index: 40; display: flex; gap: 6px; }
.sc-nav-link { font-family: var(--mono); font-size: 12px; color: var(--dim); text-decoration: none; background: var(--panel); border: 1px solid var(--bd); border-radius: 8px; padding: 6px 11px; }
.sc-nav-link:hover { color: var(--acc2); border-color: var(--acc-line); }
```

NOTE: `--mono: 'JetBrains Mono', ...` and `--ui: 'Space Grotesk', ...` are already defined in `app.css` (verified at lines 18-19), so the `var(--mono)` usages above resolve correctly.

- [ ] **Step 9: Build the web project (0 warnings gate)**

Run: `dotnet build src/SharpClient.Web/SharpClient.Web.csproj`
Expected: Build succeeded, **0 Warning(s)**, 0 errors. If any unused-using or nullable warning appears, fix it (remove the using / add `!` or null-guard) before continuing.

- [ ] **Step 10: Commit**

```bash
git add src/SharpClient.Web/WebAppStorage.cs \
        src/SharpClient.Web/WebSecretStore.cs \
        src/SharpClient.Web/DemoSessionLauncher.cs \
        src/SharpClient.Web/Components/Pages/Worlds.razor \
        src/SharpClient.Web/Program.cs \
        src/SharpClient.Web/Components/Layout/MainLayout.razor \
        src/SharpClient.Web/wwwroot/app.css \
        src/SharpClient.Web/SharpClient.Web.csproj
git commit -m "feat(web): add Worlds page and World Manager wiring

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01HHCRc5CJ6595iMzEgYYdQp"
```

---

## Task 4: Final verification + report

**Files:**
- Create: `.superpowers/sdd/lineA-worldmgr-report.md`

- [ ] **Step 1: Run all three test suites + the web build**

Run each and capture pass counts + warning counts:
```bash
dotnet run --project tests/SharpClient.Tests/SharpClient.Tests.csproj
dotnet run --project tests/SharpClient.UI.Tests/SharpClient.UI.Tests.csproj
dotnet run --project tests/SharpClient.Data.Tests/SharpClient.Data.Tests.csproj
dotnet build src/SharpClient.Web/SharpClient.Web.csproj
```
Expected:
- `SharpClient.Tests`: 75 + 8 new = **83 passing**, 0 warnings.
- `SharpClient.UI.Tests`: 8 + 3 new = **11 passing**, 0 warnings.
- `SharpClient.Data.Tests`: **16 passing** (unchanged), 0 warnings.
- `SharpClient.Web` build: **0 warnings**, 0 errors.

If any suite fails or any warning appears, STOP and fix before writing the report (re-run the relevant task's steps). Do not paper over a failure in the report.

- [ ] **Step 2: Write the report**

Create `.superpowers/sdd/lineA-worldmgr-report.md` documenting: status, the three commit SHAs + subjects (`git log --oneline -3`), the test summary for all suites + warnings, how secrets are stored (key scheme `connect:{characterId:N}`, stored via `ISecretStore`, never in domain text), what `/worlds` shows (worlds list with expandable characters, Connect/add/edit/delete, empty state, modals), anything deferred (e.g. live badges, clearing a secret via edit, real telnet launcher), and the report path.

- [ ] **Step 3: Final reply (≤15 lines)**

Reply with ONLY: Status; commit SHAs + subjects; test summary (all suites + warnings); how secrets are stored (key scheme); what `/worlds` shows; anything deferred; report path.

---

## Self-Review

**Spec coverage:**
- §1 `ISessionLauncher` (Core, with connect-string responsibility documented) → Task 1 Step 1. ✓
- §2 `WorldManagerViewModel` (ctor, Worlds/LoadAsync, world CRUD, character CRUD with secret storage, ConnectAsync, Changed event, HasWorlds) → Task 1 Steps 2/7. ✓
- §3 `WorldManager.razor` (list, expand, Connect, add/edit/delete, empty state, IDisposable Changed subscription), `WorldEditor.razor`, `CharacterEditor.razor` → Task 2 Steps 1-3. ✓
- §4 VM tests (LoadAsync, AddWorld+Changed, AddCharacter secret + plaintext-absent, Delete, ConnectAsync via fakes; `FakeWorldStore`/`FakeSessionLauncher`, reuse `FakeSecretStore`) → Task 1 Steps 4-8. Component tests (render world+char name, Connect button, empty state) → Task 2 Steps 5-7. ✓
- §5 Web host (`DemoSessionLauncher`→`DemoSession`; register `IWorldStore` EF on SQLite via `IAppStorage`, in-memory `ISecretStore`, `WorldManagerViewModel`, `ISessionLauncher`; `/worlds` page; nav; empty state reachable; connect opens a session on Home; extend app.css; 0 warnings) → Task 3 Steps 1-9. ✓
- Global constraints (net10.0, 0 warnings, nullable/implicit-usings not redeclared, file-scoped namespaces, sealed, TUnit+bUnit, temp-SQLite pattern referenced) → Global Constraints section + per-task build gates. ✓
- Verification commands + commit messages → Task 4 + per-task commit steps. ✓
- Report to `.superpowers/sdd/lineA-worldmgr-report.md` + ≤15-line reply → Task 4. ✓

**Placeholder scan:** No TBD/TODO/"handle edge cases"/"similar to" — every code step contains complete code. ✓

**Type consistency:** `WorldManagerViewModel` ctor `(IWorldStore, ISecretStore, ISessionManager, ISessionLauncher)` and method names (`LoadAsync`, `AddWorldAsync`, `UpdateWorldAsync`, `DeleteWorldAsync`, `AddCharacterAsync`, `UpdateCharacterAsync`, `DeleteCharacterAsync`, `ConnectAsync`) are identical across Task 1 (definition), Task 2 (component calls), and Task 3 (page/DI). `ISessionLauncher.LaunchAsync(World, Character, CancellationToken)` consistent across fakes, demo launcher, and VM. Secret key scheme `connect:{character.Id:N}` consistent in VM + tests. CSS class names in Task 2 components match the Task 3 `app.css` definitions and the Task 2 test selectors (`.sc-world-row`, `.sc-connect-btn`, `.sc-wm-empty-cta`). ✓

**Naming note:** the spec prose writes `Vm.AddWorld`/`UpdateWorld`/`AddCharacter`/`UpdateCharacter`; this plan uses the idiomatic `...Async` suffix for these async methods (and `ConnectAsync` per the spec). This is a deliberate, consistent interpretation applied everywhere.
