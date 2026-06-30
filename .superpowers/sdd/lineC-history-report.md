# Line C — SessionHistory FTS5 Implementation Report

**Date:** 2026-06-30  
**Branch:** feat/phase2-ui-foundation  
**Status:** DONE

---

## What was built

`SharpClient.Data.SessionHistory : ISessionHistory` — full-text search over session
history lines using SQLite FTS5 via `Microsoft.Data.Sqlite` raw SQL.

### Files created / modified

| File | Action |
|------|--------|
| `src/SharpClient.Data/SessionHistory.cs` | Created |
| `src/SharpClient.Data/SharpClient.Data.csproj` | Added `Microsoft.Data.Sqlite` 10.0.9 explicit top-level reference |
| `tests/SharpClient.Data.Tests/SessionHistoryTests.cs` | Created |
| `.superpowers/sdd/lineC-history-report.md` | Created (this file) |

---

## Design decisions

### FTS5 table schema
```sql
CREATE VIRTUAL TABLE IF NOT EXISTS session_history
USING fts5(character_id UNINDEXED, line, sequence UNINDEXED);
```
- `character_id UNINDEXED` — stored but not tokenised; exact-match filter not needed (search is global, then filtered by caller if required).
- `sequence UNINDEXED` — stored for potential future use; always written as `0` because the authoritative monotonic sequence is the FTS5 implicit `rowid`.
- `CREATE VIRTUAL TABLE IF NOT EXISTS` is called on every operation (cheap, idempotent) — avoids maintaining a mutable per-instance flag and the IDisposable burden a SemaphoreSlim would impose.

### Sequence derivation
`HistoryHit.Sequence` = FTS5 `rowid` read back in `SELECT character_id, line, rowid`. The implicit rowid is SQLite's internal auto-increment integer, which is guaranteed monotonically increasing for append-only use. No race condition because SQLite is single-writer on a local file.

### FTS5 query sanitisation
`SanitiseFtsQuery(string)` splits the user input by whitespace, doubles any embedded `"` characters (FTS5 escape convention), wraps each token in `"…"`, and joins with spaces. This produces a conjunction of FTS5 phrase literals, making all FTS5 operators (`*`, `AND`, `OR`, `NOT`, `^`, unmatched `"`) inert. Empty / whitespace-only input returns `null` and the caller returns an empty list immediately without executing a MATCH query.

Example: `foo "bar` → `"foo" """bar"` (FTS5 phrases for literal `foo` and literal `"bar`).

### Connection strategy
A fresh `SqliteConnection` is opened per operation (`await using`). This is correct for a local app with no concurrency requirements; no connection pooling or shared-connection complexity needed.

---

## Test results

| Suite | Total | Passed | Failed | Warnings |
|-------|-------|--------|--------|----------|
| `SharpClient.Data.Tests` | 16 | 16 | 0 | 0 |
| `SharpClient.Tests` | 49 | 49 | 0 | 0 |

### SessionHistoryTests coverage (7 + 1 bonus test)

1. `AppendThenSearchReturnsMatchingLinesWithCorrectCharacterId` — basic round-trip; single term; correct CharacterId.
2. `MultiWordSearchMatchesLinesContainingAllTerms` — two-term AND semantics.
3. `SearchAcrossTwoCharactersReturnsCorrectCharacterIds` — per-character ID integrity.
4. `LimitIsRespectedWhenMoreMatchesExist` — 10 inserts, limit 3 → 3 results.
5. `SearchWithNoMatchReturnsEmptyList` — no false positives.
6. `EmptyOrWhitespaceQueryReturnsEmptyListWithoutException` — empty string and whitespace-only.
7. `QueryWithFtsSpecialCharactersDoesNotThrow` — `"unclosed`, `*`, `"""` all sanitised cleanly.
8. `SequenceValuesArePositiveAndDistinctPerAppend` — rowid-based sequences are positive and unique.

---

## Concerns / notes

- FTS5 is available in `SQLitePCLRaw.lib.e_sqlite3` (the e_sqlite3 build ships with FTS5 compiled in); tests confirm it works.
- The `sequence UNINDEXED` column is written as `0`; if callers ever need the stored value to equal the rowid, a post-insert `UPDATE` would be needed. For now, `HistoryHit.Sequence` always comes from `rowid` via SELECT.
- No `NuGetAuditSuppress` was needed for `Microsoft.Data.Sqlite` — the advisory only covers `SQLitePCLRaw.lib.e_sqlite3` which is already suppressed in both csproj files.
