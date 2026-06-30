# Phases 3–6 — Shared Contracts & Parallel Workstreams

This is the **single source of truth** for the domain models and interfaces that
Plans 3–6 build against. Lock these first; then Lines A/B/C implement against them
independently. All types live in `SharpClient.Core` unless noted. `net10.0`,
nullable on, file-scoped namespaces, `TreatWarningsAsErrors`. TUnit tests.

## Domain models — `SharpClient.Core/Domain/`

EF-friendly mutable POCOs (the persistence line maps these with EF Core).

```csharp
namespace SharpClient.Core.Domain;

public enum TriggerKind { Regex, Substring }
public enum TriggerActionKind { Highlight, Send, Notify }

public sealed class World
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; }
    public List<Character> Characters { get; set; } = [];
    public List<TriggerRule> Triggers { get; set; } = [];   // world-scope
    public List<AliasRule> Aliases { get; set; } = [];      // world-scope
}

public sealed class Character
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorldId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ConnectSecretKey { get; set; }           // key into ISecretStore, NOT the secret
    public List<TriggerRule> Triggers { get; set; } = [];   // character-scope (override world)
    public List<AliasRule> Aliases { get; set; } = [];
}

public sealed class TriggerRule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public TriggerKind Kind { get; set; }
    public string Pattern { get; set; } = string.Empty;
    public TriggerActionKind Action { get; set; }
    public string ActionValue { get; set; } = string.Empty; // e.g. send text, notify template, highlight colour index
    public bool Enabled { get; set; } = true;
}

public sealed class AliasRule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Pattern { get; set; } = string.Empty;     // e.g. "^k (.+)$"
    public string Expansion { get; set; } = string.Empty;   // e.g. "kill $1"
    public bool Enabled { get; set; } = true;
}
```

## Interfaces — `SharpClient.Core/`

```csharp
// Connection/  (Phase-1/2 already has ITelnetConnection, ISession, ISessionManager)

// Persistence/IWorldStore.cs  — World aggregate CRUD (Characters/rules persisted with their World graph)
public interface IWorldStore
{
    Task<IReadOnlyList<World>> GetWorldsAsync(CancellationToken cancellationToken = default);
    Task AddWorldAsync(World world, CancellationToken cancellationToken = default);
    Task UpdateWorldAsync(World world, CancellationToken cancellationToken = default);
    Task DeleteWorldAsync(Guid worldId, CancellationToken cancellationToken = default);
}

// Persistence/ISecretStore.cs  — secrets (MAUI SecureStorage in app; fake in tests)
public interface ISecretStore
{
    Task SetAsync(string key, string secret);
    Task<string?> GetAsync(string key);
    Task RemoveAsync(string key);
}

// Persistence/ISessionHistory.cs  — append + full-text search (SQLite FTS5 in app)
public sealed record HistoryHit(Guid CharacterId, string Line, long Sequence);

public interface ISessionHistory
{
    Task AppendAsync(Guid characterId, string line, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<HistoryHit>> SearchAsync(string query, int limit = 100, CancellationToken cancellationToken = default);
}

// Triggers/ITriggerEngine.cs  — apply a flat rule list to one received line
public sealed record TriggerOutcome(
    IReadOnlyList<SharpClient.Core.Rendering.StyledSegment> Segments,
    IReadOnlyList<string> SendCommands,
    IReadOnlyList<string> Notifications);

public interface ITriggerEngine
{
    // Parse rawLine via AnsiParser, apply Highlight rules (restyle matched runs),
    // collect Send/Notify actions from matching rules. Disabled rules are ignored.
    TriggerOutcome Apply(string rawLine, IReadOnlyList<Domain.TriggerRule> rules);
}

// Triggers/IAliasEngine.cs  — expand user input before send
public interface IAliasEngine
{
    // First enabled alias whose Pattern (regex) matches `input` produces its Expansion
    // with $1..$n substituted from capture groups; no match returns input unchanged.
    string Expand(string input, IReadOnlyList<Domain.AliasRule> aliases);
}

// Platform/IAppStorage.cs  — app-data locations (MAUI FileSystem in app; temp dir in tests)
public interface IAppStorage
{
    string GetDatabasePath();   // absolute path to the SQLite db file
}

// Platform/INotifier.cs  — local notifications/toasts (MAUI in app; recorder in tests)
public interface INotifier
{
    Task NotifyAsync(string message);
}
```

## Rule scope / merge

Engines are scope-agnostic: they apply whatever flat rule list they're given.
The **effective rule list** for a live session = world rules ++ character rules,
**character wins on conflict** — assembled by the session/view-model layer, not the
engine. (Phase 6 wires this; engines just need to apply a list.)

## Test fakes (in `SharpClient.Tests`)

- `FakeSecretStore : ISecretStore` — `Dictionary<string,string>`.
- `FakeAppStorage : IAppStorage` — returns a unique temp-file path per instance.
- `FakeNotifier : INotifier` — records messages in a `List<string>`.
- (`FakeTelnetConnection`, `FakeSession` already exist.)

## Parallel workstreams (after contracts land)

- **Line A (UI, main tree):** `SharpClient.Web` Blazor Server preview host over the
  RCL with in-memory fakes; `OutputView` demo; then `SessionTabs`, `InputBar`,
  session shell, design-token CSS. bUnit + view-model tests.
- **Line B (engines, worktree):** `TriggerEngine` + `AliasEngine` impls + TUnit tests.
- **Line C (persistence, worktree):** new `SharpClient.Data` project — EF Core
  `AppDbContext`, `WorldStore : IWorldStore`, `SessionHistory : ISessionHistory`
  (FTS5 via `Microsoft.Data.Sqlite`); tests on a temp SQLite file.

Lines B and C depend only on this contracts file; they never touch UI or each
other's files. Plan 5 (negotiation: extend `ITelnetConnection` with GMCP/MSDP/NAWS
events + `ProtocolPanel`) and final DI wiring (MAUI app + web host) are sequential.
