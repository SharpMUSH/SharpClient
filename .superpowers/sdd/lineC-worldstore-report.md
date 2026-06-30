# Line C — WorldStore Implementation Report

**Date:** 2026-06-30  
**Branch:** feat/phase2-ui-foundation  
**Status:** DONE

---

## What Was Built

### New Projects

| Project | Path | Role |
|---|---|---|
| `SharpClient.Data` | `src/SharpClient.Data/` | EF Core SQLite persistence library |
| `SharpClient.Data.Tests` | `tests/SharpClient.Data.Tests/` | TUnit integration tests |

Both added to `SharpClient.slnx` under `/src/` and `/tests/` folders respectively.

### Files Created

- `src/SharpClient.Data/SharpClient.Data.csproj` — `net10.0` classlib; references `SharpClient.Core`; `Microsoft.EntityFrameworkCore.Sqlite 10.0.9`; targeted `NuGetAuditSuppress` for GHSA-2m69-gcr7-jv3q (see Warnings section).
- `src/SharpClient.Data/AppDbContext.cs` — `DbContext` with `DbSet<World>`, `DbSet<Character>`, `DbSet<TriggerRule>`, `DbSet<AliasRule>`; `OnModelCreating` configures all relationships.
- `src/SharpClient.Data/WorldStore.cs` — `IWorldStore` implementation.
- `tests/SharpClient.Data.Tests/SharpClient.Data.Tests.csproj` — TUnit executable; `OutputType=Exe`; `TestingPlatformDotnetTestSupport=true`.
- `tests/SharpClient.Data.Tests/WorldStoreTests.cs` — 8 tests with a `TempFileAppStorage` helper.

---

## EF Core Approach

### Schema

`TriggerRule` and `AliasRule` are shared entity types used by both `World` and `Character`. Two separate EF relationships are configured per entity type:

- `World → Triggers/Aliases` via **shadow FK `"WorldId"`** (nullable) on the rule tables, `OnDelete(Cascade)`.
- `Character → Triggers/Aliases` via **shadow FK `"CharacterId"`** (nullable) on the rule tables, `OnDelete(Cascade)`.

Result: `TriggerRules` and `AliasRules` tables each have two nullable FK columns (`WorldId`, `CharacterId`). A world-scope rule has `WorldId` set and `CharacterId` null; a character-scope rule is the reverse.

`World → Characters` uses the explicit `Character.WorldId` property FK.

### UpdateWorldAsync Strategy

**Delete-then-add with two `SaveChangesAsync` calls:**

1. `_db.ChangeTracker.Clear()` — detach any previously tracked entities so the subsequent load is fresh (critical when the same `AppDbContext` was used for `AddWorldAsync`; without this, EF's identity resolution returns the already-tracked modified instance and the deletion can conflict with in-memory "Added" child entities).
2. Load the existing `World` graph fully (all `Include`/`ThenInclude`).
3. `Remove(existing)` — EF's client-side cascade marks all loaded children as `Deleted`.
4. `SaveChangesAsync` — commits the deletion.
5. `_db.ChangeTracker.Clear()` — release the deleted entities.
6. `Add(world)` — attach the incoming graph as `Added`.
7. `SaveChangesAsync` — commits the insertion.

**Why two commits instead of one:** EF Core batches a same-PK DELETE and INSERT into a single command batch. SQLite's UNIQUE constraint fires before the row is deleted, causing `SqliteException: UNIQUE constraint failed`. Two commits guarantees the delete is physically committed before the insert.

**Why ChangeTracker.Clear() is needed:** After `AddWorldAsync`, the `world` object is tracked as `Unchanged`. The test mutates it (rename, add CharC). `FirstOrDefaultAsync` with identity resolution returns the same tracked (now `Modified`) instance. `Remove` then cascades to the `Added` CharC, producing an unexpected extra delete attempt. Clearing the tracker before the load prevents this.

---

## Test Results

| Suite | Total | Passed | Failed |
|---|---|---|---|
| `SharpClient.Data.Tests` | 8 | 8 | 0 |
| `SharpClient.Tests` (existing) | 49 | 49 | 0 |
| `SharpClient.UI.Tests` (existing) | 2 | 2 | 0 |

### Data.Tests coverage

- `AddWorldThenGetWorldsReturnsFullyPopulatedGraph` — full graph (2 chars, world rules, char rules), counts correct.
- `AddWorldRoundTripsTriggerRuleFields` — `Kind`, `Pattern`, `Action`, `ActionValue`, `Enabled=false`.
- `AddWorldRoundTripsAliasRuleFields` — `Pattern`, `Expansion`, `Enabled=true`.
- `AddWorldRoundTripsConnectSecretKey` — nullable string preserved.
- `UpdateWorldRenameAndAddCharacterReflectedInGetWorlds` — rename + add character.
- `UpdateWorldRemoveCharacterNotReturnedByGetWorlds` — removed child not returned.
- `DeleteWorldGetWorldsReturnsEmpty` — store empty after delete.
- `DeleteWorldCascadesChildrenNoOrphanRows` — direct counts on `Characters`, `TriggerRules`, `AliasRules` all zero.

---

## Warnings Resolved

### NU1903 — SQLitePCLRaw.lib.e_sqlite3 vulnerability (GHSA-2m69-gcr7-jv3q)

`Microsoft.EntityFrameworkCore.Sqlite 10.0.9` pulls in `SQLitePCLRaw.lib.e_sqlite3 2.1.11`, which NuGet flags as a known high-severity vulnerability. Version 2.1.11 is the **latest available release** — no patched version exists. A targeted `<NuGetAuditSuppress>` item was added to both Data and Data.Tests project files (not `Directory.Build.props`) to suppress only this specific advisory.

### CA1707 — Underscore in test method names

TUnit tests typically use `MethodName_State_Expected` naming. `AnalysisLevel=latest-recommended` includes CA1707 as an error. All test method names were written in `PascalCase` (e.g., `AddWorldRoundTripsTriggerRuleFields`) to match the existing test conventions in `SharpClient.Tests`.

### TUnitAssertions0015 — `.IsEqualTo(true/false)` deprecated

TUnit 1.57.0 requires `.IsTrue()` / `.IsFalse()` in place of `.IsEqualTo(true/false)`.

### CS9051 — File-local type in public member signature

`TempFileAppStorage` was initially declared `file sealed class`; changed to `internal sealed class`.

### CA1001 — Type owns disposable field

Keeping `AppDbContext` as a field on the test class triggers CA1001. Resolved by creating `AppDbContext` locally (via `await using`) inside each test method rather than storing it as a class field.

---

## Concerns

None. The two-SaveChanges update approach is a known limitation (two round-trips, not a single atomic transaction). For a MUSH client settings store with no concurrent writers this is acceptable. If atomicity becomes a requirement, wrapping both commits in a `BeginTransaction` / `CommitTransaction` block is a straightforward addition.
