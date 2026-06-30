# stream/tests — completion report

**Status:** Done — all tasks completed, all suites green, 0 build warnings.

**Commits (2):**
- `b47776f` polish: dedup PhosphorDefault, TriggerEngine regex cache, SessionHistory sequence column cleanup
- `45b01eb` tests: AnsiPalette, SessionManager, TriggerEngine, WorldStore, SessionHistory, AnsiParser

**Tests added (new assertions/tests):**
| Suite | Before | After | Added |
|---|---|---|---|
| SharpClient.Tests (Core) | 123 | 133 | +10 |
| SharpClient.Data.Tests | 16 | 17 | +1 |
| SharpClient.UI.Tests | 33 | 33 | 0 new, 0 broken |
| **Total** | **172** | **183** | **+11** |

**Tests added breakdown:**
- AnsiPaletteTests: +3 (grayscale end 255→#eeeeee, mid-cube 100→#878700, negative→phosphor)
- SessionManagerTests: +4 (close-non-active, activate-untracked, close-untracked, close-last-with-dispose+Changed count)
- TriggerEngineTests: +2 (256-ActionValue after valid → first kept; 256-alone → default)
- AnsiParserTests: +1 (trailing \n preserved in segment text)
- WorldStoreTests: +1 (UpdateWorldAsync removes character cascades its rules; .Count()→CountAsync() fixed)
- SessionHistoryTests: IsNotNull() → Count().IsEqualTo(0) (concretised, not a new test)

**Build:** `dotnet build src/SharpClient.Web/SharpClient.Web.csproj` — 0 errors, 0 warnings.

**UI.Tests SDK:** Changed to `Microsoft.NET.Sdk` — bUnit resolves, all 33 tests pass.

**Skipped:** Nothing skipped.

**Report:** `/home/grave/RiderProjects/SharpClient-wt-tests/.superpowers/sdd/stream-tests-report.md`
