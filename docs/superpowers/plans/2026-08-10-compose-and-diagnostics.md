# Compose Tab and Post-Crash Diagnostics Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a full-screen Compose tab that turns multi-line prose into a single escaped MUSH command line, and an in-app diagnostics log viewer with a "crashed last run" banner.

**Architecture:** Two independent slices. Compose is a pure formatter in `SharpClient.Core/Formatting` plus a `ComposeViewModel` and a Blazor page that fills the tab body. Diagnostics first moves the existing `FileLogStore`/`FileLoggerProvider` out of the MAUI head into `SharpClient.Core/Diagnostics` (so they become testable), then adds a parser, an `ILogReader` abstraction, a viewer page, and a launch-time crash banner.

**Tech Stack:** .NET 10, MAUI Blazor Hybrid + Blazor Server, TUnit (core tests), bUnit 2.7.2 (component tests).

## Global Constraints

- Target framework is `net10.0` for Core/UI/Data and their test projects; only `SharpClient.App` multi-targets the MAUI heads.
- Tests run as executables: `dotnet run --project tests/<Project>/<Project>.csproj -c Release`. **Always use `-c Release`** — that is the configuration CI uses and the one where `TreatWarningsAsErrors` gates the build. Never disable `TreatWarningsAsErrors` to get a build green; fix the warning.
- To run a single test class, append `-- --treenode-filter "/*/*/<ClassName>/*"`.
- Anything referenced by a test project must live in `SharpClient.Core`, `SharpClient.UI`, or `SharpClient.Data`. Code in `SharpClient.App` is unreachable from tests.
- View models expose plain properties plus a `public event Action? Changed`; there is no `INotifyPropertyChanged` in this codebase. Follow `SharpClient.Core/Presentation/SettingsViewModel.cs` and `SessionsViewModel.cs`.
- All new DI registrations that both hosts need go in `src/SharpClient.UI/ServiceCollectionExtensions.cs`, not in each host — host drift has caused production crashes here before.
- CSS classes are prefixed `sc-` and live in `src/SharpClient.UI/wwwroot/app.css`. Use the existing custom properties (`--acc`, `--acc2`, `--acc-soft`, `--acc-line`, `--panel`, `--outbg`, `--bd`, `--bd2`, `--tx`, `--dim`, `--faint`, `--pho`, `--mono`, `--out-fs`).
- Do not add explanatory or narration comments. Comment only non-obvious *why*, matching the density of surrounding code.
- Work on branch `feature/compose-tab-and-diagnostics`, which already exists and holds the design doc.

---

## File Structure

**Compose (Tasks 1–3)**

| File | Responsibility |
| --- | --- |
| `src/SharpClient.Core/Formatting/MushPoseFormatter.cs` | Create. `PosePrefix` enum + pure escaping/joining. No dependencies. |
| `src/SharpClient.Core/Presentation/ComposeViewModel.cs` | Create. Per-session drafts, prefix selection, per-world custom prefix, send. |
| `src/SharpClient.UI/Components/ComposeView.razor` | Create. Full-height editor / preview / footer. |
| `src/SharpClient.UI/Pages/ComposePage.razor` | Create. Route `/compose`. |
| `src/SharpClient.UI/Layout/MainLayout.razor` | Modify. Fifth nav entry. |
| `src/SharpClient.UI/ServiceCollectionExtensions.cs` | Modify. Register `ComposeViewModel`. |
| `src/SharpClient.UI/wwwroot/app.css` | Modify. `.sc-compose-*` block. |

**Diagnostics (Tasks 4–8)**

| File | Responsibility |
| --- | --- |
| `src/SharpClient.Core/Diagnostics/FileLogStore.cs` | Move from App. Directory-injected; writes log + crash marker; rotation. |
| `src/SharpClient.Core/Diagnostics/FileLoggerProvider.cs` | Move from App. Unchanged behaviour. |
| `src/SharpClient.Core/Diagnostics/LogEntry.cs` | Create. `LogEntry` and `CrashReport` records. |
| `src/SharpClient.Core/Diagnostics/LogEntryParser.cs` | Create. Raw text → entries, stack traces attached. |
| `src/SharpClient.Core/Diagnostics/ILogReader.cs` | Create. Interface + `NoopLogReader`. |
| `src/SharpClient.Core/Diagnostics/FileLogReader.cs` | Create. Reads store files through the parser. |
| `src/SharpClient.UI/Components/DiagnosticsView.razor` | Create. Filter chips, entry list, actions. |
| `src/SharpClient.UI/Pages/DiagnosticsPage.razor` | Create. Route `/diagnostics`, `?filter=` query. |
| `src/SharpClient.UI/Components/CrashBanner.razor` | Create. Launch-time banner. |
| `src/SharpClient.UI/Components/SettingsView.razor` | Modify. "View log" link. |
| `src/SharpClient.UI/wwwroot/sc-interop.js` | Modify. `copyText` export. |
| `src/SharpClient.App/MauiProgram.cs`, `Platforms/Android/MainActivity.cs`, `Services/MauiLogExporter.cs` | Modify. Namespace + constructor changes. |
| `src/SharpClient.Web/Program.cs` | Modify. Register `NoopLogReader`. |

---

### Task 1: MushPoseFormatter

**Files:**
- Create: `src/SharpClient.Core/Formatting/MushPoseFormatter.cs`
- Test: `tests/SharpClient.Tests/Formatting/MushPoseFormatterTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces:
  - `enum SharpClient.Core.Formatting.PosePrefix { Say, Pose, Semipose, Emit, Custom }`
  - `static string MushPoseFormatter.CommandFor(PosePrefix prefix, string customPrefix)`
  - `static string MushPoseFormatter.Format(string prefix, string body)`

- [ ] **Step 1: Write the failing tests**

Create `tests/SharpClient.Tests/Formatting/MushPoseFormatterTests.cs`:

```csharp
using SharpClient.Core.Formatting;

namespace SharpClient.Tests.Formatting;

public sealed class MushPoseFormatterTests
{
    [Test]
    public async Task LiteralPercentIsDoubled()
    {
        var result = MushPoseFormatter.Format("pose", "is 100% sure");
        await Assert.That(result).IsEqualTo("pose is 100%% sure");
    }

    [Test]
    public async Task NewlineBecomesPercentR()
    {
        var result = MushPoseFormatter.Format("pose", "line one\nline two");
        await Assert.That(result).IsEqualTo("pose line one%rline two");
    }

    [Test]
    public async Task PercentEscapingRunsBeforeNewlineSubstitution()
    {
        var result = MushPoseFormatter.Format("pose", "50%\nrest");
        await Assert.That(result).IsEqualTo("pose 50%%%rrest");
    }

    [Test]
    public async Task CarriageReturnLineFeedNormalisesLikeLineFeed()
    {
        var result = MushPoseFormatter.Format("pose", "a\r\nb");
        await Assert.That(result).IsEqualTo("pose a%rb");
    }

    [Test]
    public async Task LoneCarriageReturnNormalisesLikeLineFeed()
    {
        var result = MushPoseFormatter.Format("pose", "a\rb");
        await Assert.That(result).IsEqualTo("pose a%rb");
    }

    [Test]
    public async Task InteriorBlankLineBecomesTwoPercentR()
    {
        var result = MushPoseFormatter.Format("pose", "a\n\nb");
        await Assert.That(result).IsEqualTo("pose a%r%rb");
    }

    [Test]
    public async Task TrailingBlankLinesAreDropped()
    {
        var result = MushPoseFormatter.Format("pose", "a\n\n\n");
        await Assert.That(result).IsEqualTo("pose a");
    }

    [Test]
    public async Task LeadingBlankLinesAreDropped()
    {
        var result = MushPoseFormatter.Format("pose", "\n\na");
        await Assert.That(result).IsEqualTo("pose a");
    }

    [Test]
    public async Task TrailingWhitespaceIsTrimmedPerLine()
    {
        var result = MushPoseFormatter.Format("pose", "a   \nb\t");
        await Assert.That(result).IsEqualTo("pose a%rb");
    }

    [Test]
    public async Task LeadingIndentOnALineIsPreserved()
    {
        var result = MushPoseFormatter.Format("pose", "a\n   b");
        await Assert.That(result).IsEqualTo("pose a%r   b");
    }

    [Test]
    public async Task BracketsAndBackslashesPassThroughUnescaped()
    {
        var result = MushPoseFormatter.Format("pose", @"holds [a] \ thing");
        await Assert.That(result).IsEqualTo(@"pose holds [a] \ thing");
    }

    [Test]
    public async Task PrefixEndingInEqualsJoinsWithoutSpace()
    {
        var result = MushPoseFormatter.Format("page Bob=", "hello");
        await Assert.That(result).IsEqualTo("page Bob=hello");
    }

    [Test]
    public async Task PrefixEndingInSlashJoinsWithoutSpace()
    {
        var result = MushPoseFormatter.Format("chan/", "hello");
        await Assert.That(result).IsEqualTo("chan/hello");
    }

    [Test]
    public async Task PrefixEndingInSpaceJoinsVerbatim()
    {
        var result = MushPoseFormatter.Format("page Bob ", "hello");
        await Assert.That(result).IsEqualTo("page Bob hello");
    }

    [Test]
    public async Task BarePrefixGetsASingleSpace()
    {
        var result = MushPoseFormatter.Format("@emit", "hello");
        await Assert.That(result).IsEqualTo("@emit hello");
    }

    [Test]
    public async Task EmptyBodyYieldsPrefixOnly()
    {
        var result = MushPoseFormatter.Format("pose", string.Empty);
        await Assert.That(result).IsEqualTo("pose");
    }

    [Test]
    public async Task WhitespaceOnlyBodyYieldsPrefixOnly()
    {
        var result = MushPoseFormatter.Format("page Bob=", "  \n \n ");
        await Assert.That(result).IsEqualTo("page Bob=");
    }

    [Test]
    public async Task EmptyPrefixYieldsBodyOnly()
    {
        var result = MushPoseFormatter.Format(string.Empty, "hello");
        await Assert.That(result).IsEqualTo("hello");
    }

    [Test]
    public async Task CommandForMapsEveryBuiltIn()
    {
        await Assert.That(MushPoseFormatter.CommandFor(PosePrefix.Say, "x")).IsEqualTo("say");
        await Assert.That(MushPoseFormatter.CommandFor(PosePrefix.Pose, "x")).IsEqualTo("pose");
        await Assert.That(MushPoseFormatter.CommandFor(PosePrefix.Semipose, "x")).IsEqualTo("semipose");
        await Assert.That(MushPoseFormatter.CommandFor(PosePrefix.Emit, "x")).IsEqualTo("@emit");
    }

    [Test]
    public async Task CommandForCustomReturnsTheCustomPrefix()
    {
        await Assert.That(MushPoseFormatter.CommandFor(PosePrefix.Custom, "page Bob=")).IsEqualTo("page Bob=");
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet run --project tests/SharpClient.Tests/SharpClient.Tests.csproj -c Release -- --treenode-filter "/*/*/MushPoseFormatterTests/*"`

Expected: build failure — `The type or namespace name 'Formatting' does not exist in the namespace 'SharpClient.Core'`.

- [ ] **Step 3: Write the implementation**

Create `src/SharpClient.Core/Formatting/MushPoseFormatter.cs`:

```csharp
namespace SharpClient.Core.Formatting;

public enum PosePrefix
{
    Say,
    Pose,
    Semipose,
    Emit,
    Custom,
}

/// <summary>
/// Turns multi-line prose into the single line a MUSH expects: literal percent signs are doubled so
/// the server does not treat them as substitutions, and line breaks become <c>%r</c>.
/// </summary>
public static class MushPoseFormatter
{
    public static string CommandFor(PosePrefix prefix, string customPrefix) => prefix switch
    {
        PosePrefix.Say => "say",
        PosePrefix.Pose => "pose",
        PosePrefix.Semipose => "semipose",
        PosePrefix.Emit => "@emit",
        _ => customPrefix,
    };

    public static string Format(string prefix, string body) => Join(prefix, EscapeBody(body));

    private static string EscapeBody(string body)
    {
        var lines = body.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

        var first = 0;
        var last = lines.Length - 1;
        while (first <= last && lines[first].Trim().Length == 0)
        {
            first++;
        }

        while (last >= first && lines[last].Trim().Length == 0)
        {
            last--;
        }

        if (first > last)
        {
            return string.Empty;
        }

        // Escape per line, then join with %r: the substitution markers must not be escaped themselves.
        var escaped = new string[last - first + 1];
        for (var i = first; i <= last; i++)
        {
            escaped[i - first] = lines[i].TrimEnd().Replace("%", "%%");
        }

        return string.Join("%r", escaped);
    }

    private static string Join(string prefix, string body)
    {
        if (prefix.Length == 0)
        {
            return body;
        }

        if (body.Length == 0)
        {
            return prefix.TrimEnd();
        }

        var last = prefix[^1];
        return last is '=' or '/' || char.IsWhiteSpace(last)
            ? prefix + body
            : prefix + " " + body;
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet run --project tests/SharpClient.Tests/SharpClient.Tests.csproj -c Release -- --treenode-filter "/*/*/MushPoseFormatterTests/*"`

Expected: all 20 tests pass.

- [ ] **Step 5: Run the whole core suite**

Run: `dotnet run --project tests/SharpClient.Tests/SharpClient.Tests.csproj -c Release`

Expected: everything passes, no new warnings.

- [ ] **Step 6: Commit**

```bash
git add src/SharpClient.Core/Formatting/MushPoseFormatter.cs tests/SharpClient.Tests/Formatting/MushPoseFormatterTests.cs
git commit -m "feat(compose): MUSH pose formatter with %-escaping and %r line joining"
```

---

### Task 2: ComposeViewModel

**Files:**
- Create: `src/SharpClient.Core/Presentation/ComposeViewModel.cs`
- Modify: `src/SharpClient.UI/ServiceCollectionExtensions.cs`
- Test: `tests/SharpClient.Tests/Presentation/ComposeViewModelTests.cs`

**Interfaces:**
- Consumes: `PosePrefix`, `MushPoseFormatter.CommandFor`, `MushPoseFormatter.Format` from Task 1. `ISessionManager` (`Sessions`, `Active`, `Activate`, `CloseAsync`, `Changed`) and `ISession` (`State`, `WorldId`, `SendAsync`, `StateChanged`) already exist.
- Produces: `ComposeViewModel` with `Active`, `SelectedPrefix`, `CustomPrefix`, `Body`, `Command`, `Preview`, `CanSend`, `SendAsync()`, `Clear()`, `event Action? Changed`.

- [ ] **Step 1: Write the failing tests**

Create `tests/SharpClient.Tests/Presentation/ComposeViewModelTests.cs`. `FakeSession` (in `tests/SharpClient.Tests/Sessions/FakeSession.cs`) has settable `State` and `WorldId`, a `Sent` list, and a `RaiseState` helper; `FakePreferences` is in `tests/SharpClient.Tests/Fakes/`.

```csharp
using SharpClient.Core.Connection;
using SharpClient.Core.Formatting;
using SharpClient.Core.Presentation;
using SharpClient.Core.Sessions;
using SharpClient.Tests.Fakes;
using SharpClient.Tests.Sessions;

namespace SharpClient.Tests.Presentation;

public sealed class ComposeViewModelTests
{
    private static (ComposeViewModel vm, SessionManager mgr, FakePreferences prefs) Build()
    {
        var mgr = new SessionManager();
        var prefs = new FakePreferences();
        return (new ComposeViewModel(mgr, prefs), mgr, prefs);
    }

    [Test]
    public async Task CannotSendWithNoSession()
    {
        var (vm, _, _) = Build();
        await Assert.That(vm.CanSend).IsFalse();
    }

    [Test]
    public async Task CannotSendWhenDisconnected()
    {
        var (vm, mgr, _) = Build();
        var s = new FakeSession { State = ConnectionState.Disconnected };
        mgr.Add(s);
        vm.Body = "waves";

        await Assert.That(vm.CanSend).IsFalse();
    }

    [Test]
    public async Task CannotSendWhenBodyIsBlank()
    {
        var (vm, mgr, _) = Build();
        mgr.Add(new FakeSession { State = ConnectionState.Connected });
        vm.Body = "   ";

        await Assert.That(vm.CanSend).IsFalse();
    }

    [Test]
    public async Task CannotSendWhenCustomPrefixIsBlank()
    {
        var (vm, mgr, _) = Build();
        mgr.Add(new FakeSession { State = ConnectionState.Connected });
        vm.Body = "waves";
        vm.SelectedPrefix = PosePrefix.Custom;
        vm.CustomPrefix = "  ";

        await Assert.That(vm.CanSend).IsFalse();
    }

    [Test]
    public async Task CanSendWhenConnectedWithBody()
    {
        var (vm, mgr, _) = Build();
        mgr.Add(new FakeSession { State = ConnectionState.Connected });
        vm.Body = "waves";

        await Assert.That(vm.CanSend).IsTrue();
    }

    [Test]
    public async Task PreviewUsesSelectedPrefixAndEscapes()
    {
        var (vm, mgr, _) = Build();
        mgr.Add(new FakeSession { State = ConnectionState.Connected });
        vm.SelectedPrefix = PosePrefix.Emit;
        vm.Body = "50% off\nsecond line";

        await Assert.That(vm.Preview).IsEqualTo("@emit 50%% off%rsecond line");
    }

    [Test]
    public async Task SendDeliversFormattedLineAndClearsDraft()
    {
        var (vm, mgr, _) = Build();
        var s = new FakeSession { State = ConnectionState.Connected };
        mgr.Add(s);
        vm.Body = "grins\nwidely";

        await vm.SendAsync();

        await Assert.That(s.Sent).Contains("pose grins%rwidely");
        await Assert.That(vm.Body).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task SendDoesNothingWhenCannotSend()
    {
        var (vm, mgr, _) = Build();
        var s = new FakeSession { State = ConnectionState.Disconnected };
        mgr.Add(s);
        vm.Body = "grins";

        await vm.SendAsync();

        await Assert.That(s.Sent).IsEmpty();
    }

    [Test]
    public async Task DraftsAreKeptPerSession()
    {
        var (vm, mgr, _) = Build();
        var a = new FakeSession { State = ConnectionState.Connected };
        var b = new FakeSession { State = ConnectionState.Connected };
        mgr.Add(a);
        vm.Body = "draft for a";
        mgr.Add(b);
        vm.Body = "draft for b";

        mgr.Activate(a);
        await Assert.That(vm.Body).IsEqualTo("draft for a");

        mgr.Activate(b);
        await Assert.That(vm.Body).IsEqualTo("draft for b");
    }

    [Test]
    public async Task DraftsArePrunedWhenSessionCloses()
    {
        var (vm, mgr, _) = Build();
        var a = new FakeSession { State = ConnectionState.Connected };
        mgr.Add(a);
        vm.Body = "draft for a";

        await mgr.CloseAsync(a);

        await Assert.That(vm.TrackedDraftCount).IsEqualTo(0);
    }

    [Test]
    public async Task CustomPrefixPersistsPerWorld()
    {
        var worldA = Guid.NewGuid();
        var worldB = Guid.NewGuid();
        var (vm, mgr, prefs) = Build();
        var a = new FakeSession { State = ConnectionState.Connected, WorldId = worldA };
        var b = new FakeSession { State = ConnectionState.Connected, WorldId = worldB };

        mgr.Add(a);
        vm.SelectedPrefix = PosePrefix.Custom;
        vm.CustomPrefix = "page Alice=";

        mgr.Add(b);
        vm.CustomPrefix = "page Bob=";

        await Assert.That(prefs.GetString($"compose.custom.{worldA}", "")).IsEqualTo("page Alice=");
        await Assert.That(prefs.GetString($"compose.custom.{worldB}", "")).IsEqualTo("page Bob=");
    }

    [Test]
    public async Task CustomPrefixIsReloadedWhenActiveSessionChanges()
    {
        var worldA = Guid.NewGuid();
        var worldB = Guid.NewGuid();
        var (vm, mgr, _) = Build();
        var a = new FakeSession { State = ConnectionState.Connected, WorldId = worldA };
        var b = new FakeSession { State = ConnectionState.Connected, WorldId = worldB };

        mgr.Add(a);
        vm.CustomPrefix = "page Alice=";
        mgr.Add(b);
        vm.CustomPrefix = "page Bob=";

        mgr.Activate(a);

        await Assert.That(vm.CustomPrefix).IsEqualTo("page Alice=");
    }

    [Test]
    public async Task ChangedFiresWhenConnectionStateChanges()
    {
        var (vm, mgr, _) = Build();
        var s = new FakeSession { State = ConnectionState.Disconnected };
        mgr.Add(s);
        var fired = 0;
        vm.Changed += () => fired++;

        s.RaiseState(ConnectionState.Connected);

        await Assert.That(fired).IsEqualTo(1);
        await Assert.That(vm.CanSend).IsFalse();
    }

    [Test]
    public async Task ClearEmptiesOnlyTheActiveDraft()
    {
        var (vm, mgr, _) = Build();
        var a = new FakeSession { State = ConnectionState.Connected };
        var b = new FakeSession { State = ConnectionState.Connected };
        mgr.Add(a);
        vm.Body = "keep me";
        mgr.Add(b);
        vm.Body = "drop me";

        vm.Clear();

        await Assert.That(vm.Body).IsEqualTo(string.Empty);
        mgr.Activate(a);
        await Assert.That(vm.Body).IsEqualTo("keep me");
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet run --project tests/SharpClient.Tests/SharpClient.Tests.csproj -c Release -- --treenode-filter "/*/*/ComposeViewModelTests/*"`

Expected: build failure — `ComposeViewModel` does not exist.

- [ ] **Step 3: Write the implementation**

Create `src/SharpClient.Core/Presentation/ComposeViewModel.cs`:

```csharp
using SharpClient.Core.Connection;
using SharpClient.Core.Formatting;
using SharpClient.Core.Platform;
using SharpClient.Core.Sessions;

namespace SharpClient.Core.Presentation;

public sealed class ComposeViewModel
{
    private readonly ISessionManager _manager;
    private readonly IPreferences _prefs;
    private readonly Dictionary<ISession, string> _drafts = [];
    private ISession? _activeSession;
    private PosePrefix _selectedPrefix = PosePrefix.Pose;
    private string _customPrefix = string.Empty;

    public ComposeViewModel(ISessionManager manager, IPreferences prefs)
    {
        _manager = manager;
        _prefs = prefs;
        _manager.Changed += OnManagerChanged;
        TrackActiveSession(_manager.Active);
    }

    internal int TrackedDraftCount => _drafts.Count;

    public event Action? Changed;

    public ISession? Active => _manager.Active;

    public PosePrefix SelectedPrefix
    {
        get => _selectedPrefix;
        set
        {
            _selectedPrefix = value;
            Changed?.Invoke();
        }
    }

    public string CustomPrefix
    {
        get => _customPrefix;
        set
        {
            _customPrefix = value;
            if (Active is not null)
            {
                _prefs.SetString(CustomPrefixKey(Active.WorldId), value);
            }

            Changed?.Invoke();
        }
    }

    public string Body
    {
        get => Active is not null && _drafts.TryGetValue(Active, out var draft) ? draft : string.Empty;
        set
        {
            if (Active is null)
            {
                return;
            }

            _drafts[Active] = value;
            Changed?.Invoke();
        }
    }

    public string Command => MushPoseFormatter.CommandFor(_selectedPrefix, _customPrefix);

    public string Preview => MushPoseFormatter.Format(Command, Body);

    public bool CanSend =>
        Active?.State == ConnectionState.Connected
        && !string.IsNullOrWhiteSpace(Body)
        && !string.IsNullOrWhiteSpace(Command);

    public async Task SendAsync()
    {
        if (!CanSend || Active is null)
        {
            return;
        }

        var active = Active;
        var line = Preview;
        await active.SendAsync(line);

        _drafts[active] = string.Empty;
        Changed?.Invoke();
    }

    public void Clear()
    {
        if (Active is null)
        {
            return;
        }

        _drafts[Active] = string.Empty;
        Changed?.Invoke();
    }

    internal static string CustomPrefixKey(Guid worldId) => $"compose.custom.{worldId}";

    private void OnManagerChanged()
    {
        var sessions = _manager.Sessions;
        foreach (var key in _drafts.Keys.Where(k => !sessions.Contains(k)).ToList())
        {
            _drafts.Remove(key);
        }

        TrackActiveSession(_manager.Active);
        Changed?.Invoke();
    }

    private void TrackActiveSession(ISession? newActive)
    {
        if (ReferenceEquals(newActive, _activeSession))
        {
            return;
        }

        if (_activeSession is not null)
        {
            _activeSession.StateChanged -= OnActiveStateChanged;
        }

        _activeSession = newActive;

        if (_activeSession is not null)
        {
            _activeSession.StateChanged += OnActiveStateChanged;
            _customPrefix = _prefs.GetString(CustomPrefixKey(_activeSession.WorldId), string.Empty);
        }
    }

    private void OnActiveStateChanged(ConnectionState _) => Changed?.Invoke();
}
```

`TrackedDraftCount` and `CustomPrefixKey` are `internal`; `SharpClient.Core` already exposes internals to `SharpClient.Tests` via the `InternalsVisibleTo` attribute at the top of `SessionsViewModel.cs`.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet run --project tests/SharpClient.Tests/SharpClient.Tests.csproj -c Release -- --treenode-filter "/*/*/ComposeViewModelTests/*"`

Expected: all 14 tests pass.

- [ ] **Step 5: Register the view model for both hosts**

In `src/SharpClient.UI/ServiceCollectionExtensions.cs`, add after the `SettingsViewModel` registration:

```csharp
        services.AddSingleton<ComposeViewModel>(sp =>
            new ComposeViewModel(
                sp.GetRequiredService<ISessionManager>(),
                sp.GetRequiredService<IPreferences>()));
```

Update the method's XML doc, which currently reads "Registers all six presentation view models. The three session/settings view models are always singletons" — it is now seven view models and four always-singleton ones.

- [ ] **Step 6: Verify both hosts still resolve their graph**

Run: `dotnet run --project tests/SharpClient.Tests/SharpClient.Tests.csproj -c Release`

Expected: all pass, including `RuntimeWiringTests`.

- [ ] **Step 7: Commit**

```bash
git add src/SharpClient.Core/Presentation/ComposeViewModel.cs src/SharpClient.UI/ServiceCollectionExtensions.cs tests/SharpClient.Tests/Presentation/ComposeViewModelTests.cs
git commit -m "feat(compose): ComposeViewModel with per-session drafts and per-world custom prefix"
```

---

### Task 3: Compose page, nav entry, and styles

**Files:**
- Create: `src/SharpClient.UI/Components/ComposeView.razor`
- Create: `src/SharpClient.UI/Pages/ComposePage.razor`
- Modify: `src/SharpClient.UI/Layout/MainLayout.razor`
- Modify: `src/SharpClient.UI/wwwroot/app.css`
- Test: `tests/SharpClient.UI.Tests/ComposeViewTests.cs`

**Interfaces:**
- Consumes: `ComposeViewModel` and `PosePrefix` from Tasks 1–2.
- Produces: component `ComposeView` with `[Parameter] ComposeViewModel Vm`; route `/compose`; CSS classes `.sc-compose`, `.sc-compose-chip`, `.sc-compose-chip-active`, `.sc-compose-custom`, `.sc-compose-input`, `.sc-compose-preview`, `.sc-compose-clear`, `.sc-compose-toggle`, `.sc-compose-count`, `.sc-compose-hint`.

- [ ] **Step 1: Write the failing tests**

Create `tests/SharpClient.UI.Tests/ComposeViewTests.cs`. `UiFakeSession` has settable `State`/`WorldId` and a `Sent` list; copy the `LocalFakePrefs` pattern already used by `SettingsViewTests.cs`.

```csharp
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using SharpClient.Core.Connection;
using SharpClient.Core.Formatting;
using SharpClient.Core.Presentation;
using SharpClient.Core.Sessions;
using SharpClient.UI.Components;

namespace SharpClient.UI.Tests;

file sealed class ComposeFakePrefs : SharpClient.Core.Platform.IPreferences
{
    private readonly Dictionary<string, string> _store = [];
    public string GetString(string key, string def) => _store.TryGetValue(key, out var v) ? v : def;
    public void SetString(string key, string value) => _store[key] = value;
    public int GetInt(string key, int def) => _store.TryGetValue(key, out var v) && int.TryParse(v, out var i) ? i : def;
    public void SetInt(string key, int value) => _store[key] = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    public bool GetBool(string key, bool def) => _store.TryGetValue(key, out var v) && bool.TryParse(v, out var b) ? b : def;
    public void SetBool(string key, bool value) => _store[key] = value.ToString();
}

public sealed class ComposeViewTests
{
    private static (ComposeViewModel vm, UiFakeSession session) Connected()
    {
        var mgr = new SessionManager();
        var session = new UiFakeSession { State = ConnectionState.Connected };
        mgr.Add(session);
        return (new ComposeViewModel(mgr, new ComposeFakePrefs()), session);
    }

    [Test]
    public async Task SendIsDisabledWhenBodyIsEmpty()
    {
        using var ctx = new BunitContext();
        var (vm, _) = Connected();

        var cut = ctx.Render<ComposeView>(p => p.Add(c => c.Vm, vm));

        await Assert.That(cut.Find(".sc-send-btn").HasAttribute("disabled")).IsTrue();
    }

    [Test]
    public async Task SendIsEnabledWhenBodyIsSet()
    {
        using var ctx = new BunitContext();
        var (vm, _) = Connected();
        vm.Body = "waves";

        var cut = ctx.Render<ComposeView>(p => p.Add(c => c.Vm, vm));

        await Assert.That(cut.Find(".sc-send-btn").HasAttribute("disabled")).IsFalse();
    }

    [Test]
    public async Task ClickingSendDeliversTheFormattedLine()
    {
        using var ctx = new BunitContext();
        var (vm, session) = Connected();
        vm.Body = "grins\n100% sure";

        var cut = ctx.Render<ComposeView>(p => p.Add(c => c.Vm, vm));
        await cut.Find(".sc-send-btn").ClickAsync(new MouseEventArgs());

        await Assert.That(session.Sent).Contains("pose grins%r100%% sure");
        await Assert.That(vm.Body).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task PreviewToggleShowsTheWireTextThenReturnsToTheEditor()
    {
        using var ctx = new BunitContext();
        var (vm, _) = Connected();
        vm.Body = "grins";

        var cut = ctx.Render<ComposeView>(p => p.Add(c => c.Vm, vm));
        await cut.Find(".sc-compose-toggle").ClickAsync(new MouseEventArgs());

        await Assert.That(cut.Find(".sc-compose-preview").TextContent).IsEqualTo("pose grins");
        await Assert.That(cut.FindAll(".sc-compose-input")).IsEmpty();

        await cut.Find(".sc-compose-toggle").ClickAsync(new MouseEventArgs());

        await Assert.That(cut.FindAll(".sc-compose-input")).HasCount().EqualTo(1);
        await Assert.That(vm.Body).IsEqualTo("grins");
    }

    [Test]
    public async Task SelectingAChipChangesThePreviewedCommand()
    {
        using var ctx = new BunitContext();
        var (vm, _) = Connected();
        vm.Body = "waves";

        var cut = ctx.Render<ComposeView>(p => p.Add(c => c.Vm, vm));
        var sayChip = cut.FindAll(".sc-compose-chip")[0];
        await sayChip.ClickAsync(new MouseEventArgs());

        await Assert.That(vm.SelectedPrefix).IsEqualTo(PosePrefix.Say);
        await cut.Find(".sc-compose-toggle").ClickAsync(new MouseEventArgs());
        await Assert.That(cut.Find(".sc-compose-preview").TextContent).IsEqualTo("say waves");
    }

    [Test]
    public async Task CustomChipRevealsThePrefixField()
    {
        using var ctx = new BunitContext();
        var (vm, _) = Connected();

        var cut = ctx.Render<ComposeView>(p => p.Add(c => c.Vm, vm));
        await Assert.That(cut.FindAll(".sc-compose-custom")).IsEmpty();

        var customChip = cut.FindAll(".sc-compose-chip")[4];
        await customChip.ClickAsync(new MouseEventArgs());

        await Assert.That(cut.FindAll(".sc-compose-custom")).HasCount().EqualTo(1);
    }

    [Test]
    public async Task HintIsShownWhenNoSessionIsConnected()
    {
        using var ctx = new BunitContext();
        var mgr = new SessionManager();
        mgr.Add(new UiFakeSession { State = ConnectionState.Disconnected });
        var vm = new ComposeViewModel(mgr, new ComposeFakePrefs());

        var cut = ctx.Render<ComposeView>(p => p.Add(c => c.Vm, vm));

        await Assert.That(cut.FindAll(".sc-compose-hint")).HasCount().EqualTo(1);
    }

    [Test]
    public async Task ClearEmptiesTheEditor()
    {
        using var ctx = new BunitContext();
        var (vm, _) = Connected();
        vm.Body = "waves";

        var cut = ctx.Render<ComposeView>(p => p.Add(c => c.Vm, vm));
        await cut.Find(".sc-compose-clear").ClickAsync(new MouseEventArgs());

        await Assert.That(vm.Body).IsEqualTo(string.Empty);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet run --project tests/SharpClient.UI.Tests/SharpClient.UI.Tests.csproj -c Release -- --treenode-filter "/*/*/ComposeViewTests/*"`

Expected: build failure — `ComposeView` does not exist.

- [ ] **Step 3: Write the component**

Create `src/SharpClient.UI/Components/ComposeView.razor`:

```razor
@using SharpClient.Core.Connection
@using SharpClient.Core.Formatting
@using SharpClient.Core.Presentation
@implements IDisposable

<div class="sc-compose">
    <div class="sc-compose-prefixes">
        @foreach (var option in PrefixOptions)
        {
            var value = option.Value;
            <button type="button"
                    class="sc-compose-chip @(Vm.SelectedPrefix == value ? "sc-compose-chip-active" : "")"
                    @onclick="() => Vm.SelectedPrefix = value">@option.Label</button>
        }
    </div>

    @if (Vm.SelectedPrefix == PosePrefix.Custom)
    {
        <input class="sc-compose-custom" type="text"
               value="@Vm.CustomPrefix"
               placeholder="page Bob="
               @oninput="e => Vm.CustomPrefix = e.Value?.ToString() ?? string.Empty" />
    }

    <div class="sc-compose-body">
        @if (_previewing)
        {
            <pre class="sc-compose-preview">@Vm.Preview</pre>
        }
        else
        {
            <textarea class="sc-compose-input"
                      placeholder="Write your pose…"
                      value="@Vm.Body"
                      @oninput="OnBodyInput"
                      @onkeydown="OnKeyDown"></textarea>
        }
    </div>

    <div class="sc-compose-footer">
        <span class="sc-compose-count">@Vm.Preview.Length chars</span>
        @if (Vm.Active?.State != ConnectionState.Connected)
        {
            <a class="sc-compose-hint" href="/session">No connected session</a>
        }
        <div class="sc-compose-actions">
            <button type="button" class="sc-rules-btn sc-compose-clear" @onclick="Clear">Clear</button>
            <button type="button" class="sc-rules-btn sc-compose-toggle" @onclick="TogglePreview">
                @(_previewing ? "Edit" : "Preview")
            </button>
            <button type="button" class="sc-send-btn" disabled="@(!Vm.CanSend)" @onclick="SendAsync">Send</button>
        </div>
    </div>
</div>

@code {
    [Parameter]
    public ComposeViewModel Vm { get; set; } = null!;

    private static readonly (string Label, PosePrefix Value)[] PrefixOptions =
    [
        ("say", PosePrefix.Say),
        ("pose", PosePrefix.Pose),
        ("semipose", PosePrefix.Semipose),
        ("@emit", PosePrefix.Emit),
        ("custom", PosePrefix.Custom),
    ];

    private bool _previewing;

    protected override void OnInitialized() => Vm.Changed += OnChanged;

    private void OnChanged() => InvokeAsync(StateHasChanged);

    private void OnBodyInput(ChangeEventArgs e) => Vm.Body = e.Value?.ToString() ?? string.Empty;

    private void TogglePreview() => _previewing = !_previewing;

    private void Clear()
    {
        Vm.Clear();
        _previewing = false;
    }

    // Ctrl/Cmd+Enter sends; a bare Enter has to stay a newline in a multi-line composer.
    private async Task OnKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter" && (e.CtrlKey || e.MetaKey))
        {
            await SendAsync();
        }
    }

    private async Task SendAsync()
    {
        await Vm.SendAsync();
        _previewing = false;
        StateHasChanged();
    }

    public void Dispose() => Vm.Changed -= OnChanged;
}
```

- [ ] **Step 4: Write the page**

Create `src/SharpClient.UI/Pages/ComposePage.razor`:

```razor
@page "/compose"

@inject ComposeViewModel Vm

<PageTitle>SharpClient &middot; Compose</PageTitle>

<ComposeView Vm="@Vm" />
```

- [ ] **Step 5: Add the nav entry**

In `src/SharpClient.UI/Layout/MainLayout.razor`, insert between the `/session` and `/history` `NavLink`s:

```razor
        <NavLink class="sc-nav-link" href="/compose" Match="NavLinkMatch.Prefix">
            <svg viewBox="0 0 20 20" fill="none" stroke="currentColor" stroke-width="1.6" aria-hidden="true">
                <path d="M13.2 3.6l3.2 3.2-8.6 8.6-4 .8.8-4z" stroke-linecap="round" stroke-linejoin="round" />
                <line x1="11.6" y1="5.2" x2="14.8" y2="8.4" stroke-linecap="round" />
            </svg>
            <span>Compose</span>
        </NavLink>
```

- [ ] **Step 6: Add the styles**

Append to `src/SharpClient.UI/wwwroot/app.css`:

```css
/* ── Compose page ──────────────────────────────────────────────── */
.sc-compose { height: 100%; display: flex; flex-direction: column; gap: 10px; padding: 12px 12px 0; }
.sc-compose-prefixes { display: flex; flex-wrap: wrap; gap: 6px; }
.sc-compose-chip { font-family: var(--mono); font-size: 12px; color: var(--dim); background: var(--panel); border: 1px solid var(--bd); border-radius: 8px; padding: 6px 11px; cursor: pointer; }
.sc-compose-chip-active { color: var(--acc2); background: var(--acc-soft); border-color: var(--acc-line); }
.sc-compose-custom { font-family: var(--mono); font-size: 12px; color: var(--tx); background: var(--outbg); border: 1px solid var(--bd2); border-radius: 8px; padding: 8px 10px; }
.sc-compose-body { flex: 1; min-height: 0; display: flex; }
.sc-compose-input,
.sc-compose-preview { flex: 1; min-width: 0; font-family: var(--mono); font-size: var(--out-fs); line-height: 1.5; color: var(--tx); background: var(--outbg); border: 1px solid var(--bd2); border-radius: 10px; padding: 10px 12px; overflow-y: auto; }
.sc-compose-input { resize: none; }
.sc-compose-preview { white-space: pre-wrap; overflow-wrap: anywhere; color: var(--pho); }
.sc-compose-input:focus,
.sc-compose-custom:focus { outline: none; border-color: var(--acc-line); }
.sc-compose-footer { display: flex; align-items: center; gap: 10px; padding: 10px 0 12px; }
.sc-compose-count { font-family: var(--mono); font-size: 11px; color: var(--faint); }
.sc-compose-hint { font-family: var(--mono); font-size: 11px; color: var(--dim); text-decoration: none; }
.sc-compose-actions { margin-left: auto; display: flex; gap: 8px; }
```

- [ ] **Step 7: Run the tests to verify they pass**

Run: `dotnet run --project tests/SharpClient.UI.Tests/SharpClient.UI.Tests.csproj -c Release -- --treenode-filter "/*/*/ComposeViewTests/*"`

Expected: all 8 tests pass.

- [ ] **Step 8: Run every net10.0 suite**

```bash
dotnet run --project tests/SharpClient.Tests/SharpClient.Tests.csproj -c Release
dotnet run --project tests/SharpClient.UI.Tests/SharpClient.UI.Tests.csproj -c Release
dotnet run --project tests/SharpClient.Data.Tests/SharpClient.Data.Tests.csproj -c Release
```

Expected: all pass. `SessionTabsTests` and `SettingsViewTests` must be unaffected.

- [ ] **Step 9: Commit**

```bash
git add src/SharpClient.UI/Components/ComposeView.razor src/SharpClient.UI/Pages/ComposePage.razor src/SharpClient.UI/Layout/MainLayout.razor src/SharpClient.UI/wwwroot/app.css tests/SharpClient.UI.Tests/ComposeViewTests.cs
git commit -m "feat(compose): full-height Compose tab with preview toggle"
```

---

### Task 4: Move the log store into Core

This is a pure move plus a constructor change. No behaviour changes — but the write and rotation logic gets its first tests, because it becomes reachable from a test project for the first time.

**Files:**
- Create: `src/SharpClient.Core/Diagnostics/FileLogStore.cs` (moved from `src/SharpClient.App/Services/FileLogStore.cs`)
- Create: `src/SharpClient.Core/Diagnostics/FileLoggerProvider.cs` (moved from `src/SharpClient.App/Services/FileLoggerProvider.cs`)
- Delete: `src/SharpClient.App/Services/FileLogStore.cs`, `src/SharpClient.App/Services/FileLoggerProvider.cs`
- Modify: `src/SharpClient.Core/SharpClient.Core.csproj`, `src/SharpClient.App/MauiProgram.cs`, `src/SharpClient.App/Platforms/Android/MainActivity.cs`, `src/SharpClient.App/Services/MauiLogExporter.cs`
- Test: `tests/SharpClient.Tests/Diagnostics/FileLogStoreTests.cs`

**Interfaces:**
- Consumes: nothing from earlier tasks.
- Produces: `SharpClient.Core.Diagnostics.FileLogStore` with `FileLogStore(string logDirectory)`, `string FilePath`, `string BackupPath`, `void Append(string level, string category, string message, Exception? ex = null)`, `void WriteException(string source, Exception? ex)`. Also `SharpClient.Core.Diagnostics.FileLoggerProvider(FileLogStore store, LogLevel minLevel = LogLevel.Information)`.

- [ ] **Step 1: Add the logging abstraction package to Core**

In `src/SharpClient.Core/SharpClient.Core.csproj`, add to the existing `ItemGroup`:

```xml
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="10.0.9" />
```

- [ ] **Step 2: Write the failing tests**

Create `tests/SharpClient.Tests/Diagnostics/FileLogStoreTests.cs`:

```csharp
using SharpClient.Core.Diagnostics;

namespace SharpClient.Tests.Diagnostics;

public sealed class FileLogStoreTests
{
    private static string NewTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sharpclient-tests", Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Test]
    public async Task ConstructorCreatesTheDirectory()
    {
        var dir = Path.Combine(NewTempDir(), "nested", "logs");

        var store = new FileLogStore(dir);

        await Assert.That(Directory.Exists(dir)).IsTrue();
        await Assert.That(store.FilePath).IsEqualTo(Path.Combine(dir, "sharpclient.log"));
    }

    [Test]
    public async Task AppendWritesLevelCategoryAndMessage()
    {
        var store = new FileLogStore(NewTempDir());

        store.Append("Information", "App", "hello");

        var text = File.ReadAllText(store.FilePath);
        await Assert.That(text).Contains("[Information]");
        await Assert.That(text).Contains("App: hello");
    }

    [Test]
    public async Task AppendOmitsTheCategorySeparatorWhenCategoryIsEmpty()
    {
        var store = new FileLogStore(NewTempDir());

        store.Append("Information", string.Empty, "bare");

        await Assert.That(File.ReadAllText(store.FilePath)).Contains("[Information] bare");
    }

    [Test]
    public async Task AppendIncludesExceptionDetailOnFollowingLines()
    {
        var store = new FileLogStore(NewTempDir());
        var ex = new InvalidOperationException("boom");

        store.Append("Error", "Session", "failed", ex);

        var text = File.ReadAllText(store.FilePath);
        await Assert.That(text).Contains("Session: failed");
        await Assert.That(text).Contains("System.InvalidOperationException: boom");
    }

    [Test]
    public async Task WriteExceptionRecordsACrashLevelEntry()
    {
        var store = new FileLogStore(NewTempDir());

        store.WriteException("AppDomain", new InvalidOperationException("boom"));

        var text = File.ReadAllText(store.FilePath);
        await Assert.That(text).Contains("[CRASH]");
        await Assert.That(text).Contains("AppDomain: boom");
    }

    [Test]
    public async Task WriteExceptionToleratesANullException()
    {
        var store = new FileLogStore(NewTempDir());

        store.WriteException("AppDomain", null);

        await Assert.That(File.ReadAllText(store.FilePath)).Contains("(no exception object)");
    }

    [Test]
    public async Task OversizeLogRotatesIntoTheBackupFile()
    {
        var store = new FileLogStore(NewTempDir());
        var big = new string('x', 600 * 1024);

        store.Append("Information", "App", big);
        store.Append("Information", "App", "after rotation");

        await Assert.That(File.Exists(store.BackupPath)).IsTrue();
        await Assert.That(File.ReadAllText(store.BackupPath)).Contains(big);
        await Assert.That(File.ReadAllText(store.FilePath)).Contains("after rotation");
        await Assert.That(File.ReadAllText(store.FilePath)).DoesNotContain(big);
    }

    [Test]
    public async Task RotationReplacesAnExistingBackup()
    {
        var store = new FileLogStore(NewTempDir());
        var big = new string('x', 600 * 1024);

        store.Append("Information", "App", "first generation");
        store.Append("Information", "App", big);
        store.Append("Information", "App", "second generation trigger");
        store.Append("Information", "App", big);
        store.Append("Information", "App", "final");

        await Assert.That(File.ReadAllText(store.BackupPath)).DoesNotContain("first generation");
        await Assert.That(File.ReadAllText(store.FilePath)).Contains("final");
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet run --project tests/SharpClient.Tests/SharpClient.Tests.csproj -c Release -- --treenode-filter "/*/*/FileLogStoreTests/*"`

Expected: build failure — `SharpClient.Core.Diagnostics.FileLogStore` does not exist.

- [ ] **Step 4: Move the store**

Create `src/SharpClient.Core/Diagnostics/FileLogStore.cs` — the existing `src/SharpClient.App/Services/FileLogStore.cs` with a changed namespace and constructor, then delete the App copy:

```csharp
using System.Globalization;
using System.Text;

namespace SharpClient.Core.Diagnostics;

/// <summary>
/// Thread-safe, append-only diagnostics log written to a caller-supplied directory, with simple
/// size-based rotation (current file + one rolled backup). It is the single sink for both
/// <see cref="FileLoggerProvider"/> (framework/app <c>ILogger</c> output) and the global
/// unhandled-exception hooks, so a crash that takes the process down still leaves its stack trace on
/// disk.
/// </summary>
public sealed class FileLogStore
{
    // Keep the log small enough to share over chat but large enough to hold the lead-up to a crash.
    private const long MaxBytes = 512 * 1024;

    private readonly object _gate = new();

    /// <summary>Absolute path to the active log file.</summary>
    public string FilePath { get; }

    /// <summary>Absolute path to the single rolled-over backup.</summary>
    public string BackupPath { get; }

    public FileLogStore(string logDirectory)
    {
        Directory.CreateDirectory(logDirectory);
        FilePath = Path.Combine(logDirectory, "sharpclient.log");
        BackupPath = FilePath + ".1";
    }

    /// <summary>Appends a single timestamped entry. Never throws — logging must not crash the app.</summary>
    public void Append(string level, string category, string message, Exception? ex = null)
        => Write(FormatEntry(level, category, message, ex));

    /// <summary>Records an unhandled exception captured by one of the global hooks.</summary>
    public void WriteException(string source, Exception? ex)
        => Write(FormatEntry("CRASH", source, ex?.Message ?? "(no exception object)", ex));

    private static string FormatEntry(string level, string category, string message, Exception? ex)
    {
        var sb = new StringBuilder(256);
        sb.Append(DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz", CultureInfo.InvariantCulture));
        sb.Append(" [").Append(level).Append("] ");
        if (!string.IsNullOrEmpty(category))
        {
            sb.Append(category).Append(": ");
        }

        sb.Append(message);
        if (ex is not null)
        {
            sb.Append('\n').Append(ex);
        }

        sb.Append('\n');
        return sb.ToString();
    }

    private void Write(string text)
    {
        lock (_gate)
        {
            try
            {
                RotateIfNeeded();
                File.AppendAllText(FilePath, text);
            }
            catch
            {
                // Swallow: a failed log write must never propagate into the running app.
            }
        }
    }

    private void RotateIfNeeded()
    {
        try
        {
            var fi = new FileInfo(FilePath);
            if (!fi.Exists || fi.Length <= MaxBytes)
            {
                return;
            }

            if (File.Exists(BackupPath))
            {
                File.Delete(BackupPath);
            }

            File.Move(FilePath, BackupPath);
        }
        catch
        {
            // If rotation fails, fall through and keep appending to the existing file.
        }
    }
}
```

- [ ] **Step 5: Move the logger provider**

Create `src/SharpClient.Core/Diagnostics/FileLoggerProvider.cs` with the exact contents of `src/SharpClient.App/Services/FileLoggerProvider.cs`, changing only the namespace to `SharpClient.Core.Diagnostics`. Then delete the App copy.

- [ ] **Step 6: Update the MAUI host**

In `src/SharpClient.App/MauiProgram.cs`, `SharpClient.Core.Diagnostics` is already imported via the existing `using SharpClient.Core.Diagnostics;`. Change the construction line so the directory is supplied by the host:

```csharp
        var logStore = new FileLogStore(Path.Combine(FileSystem.AppDataDirectory, "logs"));
```

Update the comment above it — it currently says the log "lives under the app's private data dir"; keep that sentence accurate now that the path is passed in.

In `src/SharpClient.App/Platforms/Android/MainActivity.cs`, add `using SharpClient.Core.Diagnostics;` (the `using SharpClient.App.Services;` line stays only if something else in the file needs it — it does not, so remove it).

In `src/SharpClient.App/Services/MauiLogExporter.cs`, add `using SharpClient.Core.Diagnostics;`.

- [ ] **Step 7: Run the tests to verify they pass**

Run: `dotnet run --project tests/SharpClient.Tests/SharpClient.Tests.csproj -c Release -- --treenode-filter "/*/*/FileLogStoreTests/*"`

Expected: all 8 tests pass.

- [ ] **Step 8: Verify the Android head still compiles**

Run: `dotnet build src/SharpClient.App/SharpClient.App.csproj -f net10.0-android -c Release`

Expected: build succeeds. This needs JDK 17 and the maui-android workload. If the workload is unavailable in this environment, say so explicitly in the task report rather than skipping silently — CI's `android-build` job will catch it either way.

- [ ] **Step 9: Commit**

```bash
git add -A src/SharpClient.Core src/SharpClient.App tests/SharpClient.Tests/Diagnostics
git commit -m "refactor(diagnostics): move FileLogStore and FileLoggerProvider into Core"
```

---

### Task 5: LogEntryParser

**Files:**
- Create: `src/SharpClient.Core/Diagnostics/LogEntry.cs`
- Create: `src/SharpClient.Core/Diagnostics/LogEntryParser.cs`
- Test: `tests/SharpClient.Tests/Diagnostics/LogEntryParserTests.cs`

**Interfaces:**
- Consumes: the on-disk format written by `FileLogStore.FormatEntry` (Task 4).
- Produces:
  - `sealed record LogEntry(DateTimeOffset Timestamp, string Level, string Category, string Message, string? Detail)`
  - `sealed record CrashReport(DateTimeOffset Timestamp, string Source, string Message, string Detail)`
  - `static IReadOnlyList<LogEntry> LogEntryParser.Parse(string text)` — oldest first.

- [ ] **Step 1: Write the failing tests**

Create `tests/SharpClient.Tests/Diagnostics/LogEntryParserTests.cs`:

```csharp
using SharpClient.Core.Diagnostics;

namespace SharpClient.Tests.Diagnostics;

public sealed class LogEntryParserTests
{
    [Test]
    public async Task ParsesTimestampLevelCategoryAndMessage()
    {
        var entries = LogEntryParser.Parse("2026-08-10 23:41:02.123 -05:00 [Information] App: started\n");

        await Assert.That(entries).HasCount().EqualTo(1);
        await Assert.That(entries[0].Level).IsEqualTo("Information");
        await Assert.That(entries[0].Category).IsEqualTo("App");
        await Assert.That(entries[0].Message).IsEqualTo("started");
        await Assert.That(entries[0].Detail).IsNull();
        await Assert.That(entries[0].Timestamp.Offset).IsEqualTo(TimeSpan.FromHours(-5));
        await Assert.That(entries[0].Timestamp.Year).IsEqualTo(2026);
    }

    [Test]
    public async Task ParsesAnEntryWithNoCategory()
    {
        var entries = LogEntryParser.Parse("2026-08-10 23:41:02.123 -05:00 [Information] bare message\n");

        await Assert.That(entries[0].Category).IsEqualTo(string.Empty);
        await Assert.That(entries[0].Message).IsEqualTo("bare message");
    }

    [Test]
    public async Task AMessageContainingColonSpaceIsNotMistakenForACategory()
    {
        var entries = LogEntryParser.Parse("2026-08-10 23:41:02.123 -05:00 [Information] NAWS fit: cols=78\n");

        await Assert.That(entries[0].Category).IsEqualTo(string.Empty);
        await Assert.That(entries[0].Message).IsEqualTo("NAWS fit: cols=78");
    }

    [Test]
    public async Task ContinuationLinesBecomeTheDetail()
    {
        const string text = """
            2026-08-10 23:41:02.123 -05:00 [CRASH] AppDomain: boom
            System.InvalidOperationException: boom
               at SharpClient.Core.Sessions.Session.SendAsync(String line)
               at SharpClient.UI.Components.InputBar.SendAsync()

            """;

        var entries = LogEntryParser.Parse(text);

        await Assert.That(entries).HasCount().EqualTo(1);
        await Assert.That(entries[0].Message).IsEqualTo("boom");
        await Assert.That(entries[0].Detail).Contains("System.InvalidOperationException: boom");
        await Assert.That(entries[0].Detail).Contains("at SharpClient.UI.Components.InputBar.SendAsync()");
    }

    [Test]
    public async Task MultipleEntriesAreReturnedOldestFirst()
    {
        const string text = """
            2026-08-10 23:41:02.123 -05:00 [Information] App: first
            2026-08-10 23:41:03.456 -05:00 [Warning] App: second

            """;

        var entries = LogEntryParser.Parse(text);

        await Assert.That(entries).HasCount().EqualTo(2);
        await Assert.That(entries[0].Message).IsEqualTo("first");
        await Assert.That(entries[1].Message).IsEqualTo("second");
    }

    [Test]
    public async Task TextBeforeTheFirstHeaderIsDiscarded()
    {
        const string text = """
               at SomeTruncatedStackFrame()
            2026-08-10 23:41:02.123 -05:00 [Information] App: real entry

            """;

        var entries = LogEntryParser.Parse(text);

        await Assert.That(entries).HasCount().EqualTo(1);
        await Assert.That(entries[0].Message).IsEqualTo("real entry");
    }

    [Test]
    public async Task TextWithNoHeadersYieldsNoEntries()
    {
        var entries = LogEntryParser.Parse("garbage\nmore garbage\n");

        await Assert.That(entries).IsEmpty();
    }

    [Test]
    public async Task EmptyTextYieldsNoEntries()
    {
        await Assert.That(LogEntryParser.Parse(string.Empty)).IsEmpty();
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet run --project tests/SharpClient.Tests/SharpClient.Tests.csproj -c Release -- --treenode-filter "/*/*/LogEntryParserTests/*"`

Expected: build failure — `LogEntryParser` does not exist.

- [ ] **Step 3: Write the records**

Create `src/SharpClient.Core/Diagnostics/LogEntry.cs`:

```csharp
namespace SharpClient.Core.Diagnostics;

/// <summary>One parsed line of the diagnostics log; <paramref name="Detail"/> holds an attached stack trace.</summary>
public sealed record LogEntry(
    DateTimeOffset Timestamp,
    string Level,
    string Category,
    string Message,
    string? Detail);

/// <summary>The crash recorded by the previous run, surfaced at launch.</summary>
public sealed record CrashReport(
    DateTimeOffset Timestamp,
    string Source,
    string Message,
    string Detail);
```

- [ ] **Step 4: Write the parser**

Create `src/SharpClient.Core/Diagnostics/LogEntryParser.cs`:

```csharp
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace SharpClient.Core.Diagnostics;

/// <summary>Reverses <see cref="FileLogStore"/>'s line format so the log can be shown in the app.</summary>
public static partial class LogEntryParser
{
    private const string TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff zzz";

    [GeneratedRegex(
        @"^(?<ts>\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3} [+-]\d{2}:\d{2}) \[(?<level>[^\]]+)\] (?<rest>.*)$")]
    private static partial Regex HeaderPattern();

    // A category is a type name, so it never contains whitespace. Requiring that keeps a message
    // like "NAWS fit: cols=78" from being split into a bogus category.
    [GeneratedRegex(@"^(?<category>[^\s:]+): (?<message>.*)$")]
    private static partial Regex CategoryPattern();

    public static IReadOnlyList<LogEntry> Parse(string text)
    {
        var entries = new List<LogEntry>();
        if (string.IsNullOrEmpty(text))
        {
            return entries;
        }

        DateTimeOffset timestamp = default;
        var level = string.Empty;
        var category = string.Empty;
        var message = string.Empty;
        StringBuilder? detail = null;
        var open = false;

        foreach (var raw in text.Replace("\r\n", "\n").Split('\n'))
        {
            var header = HeaderPattern().Match(raw);
            if (header.Success)
            {
                if (open)
                {
                    entries.Add(Build(timestamp, level, category, message, detail));
                }

                timestamp = DateTimeOffset.ParseExact(
                    header.Groups["ts"].Value, TimestampFormat, CultureInfo.InvariantCulture);
                level = header.Groups["level"].Value;
                var rest = header.Groups["rest"].Value;
                var split = CategoryPattern().Match(rest);
                category = split.Success ? split.Groups["category"].Value : string.Empty;
                message = split.Success ? split.Groups["message"].Value : rest;
                detail = null;
                open = true;
                continue;
            }

            // Lines before the first header are the tail of an entry that rotation cut in half.
            if (!open || raw.Length == 0)
            {
                continue;
            }

            detail ??= new StringBuilder();
            if (detail.Length > 0)
            {
                detail.Append('\n');
            }

            detail.Append(raw);
        }

        if (open)
        {
            entries.Add(Build(timestamp, level, category, message, detail));
        }

        return entries;
    }

    private static LogEntry Build(
        DateTimeOffset timestamp, string level, string category, string message, StringBuilder? detail)
        => new(timestamp, level, category, message, detail?.ToString());
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet run --project tests/SharpClient.Tests/SharpClient.Tests.csproj -c Release -- --treenode-filter "/*/*/LogEntryParserTests/*"`

Expected: all 8 tests pass.

- [ ] **Step 6: Commit**

```bash
git add src/SharpClient.Core/Diagnostics/LogEntry.cs src/SharpClient.Core/Diagnostics/LogEntryParser.cs tests/SharpClient.Tests/Diagnostics/LogEntryParserTests.cs
git commit -m "feat(diagnostics): parse the on-disk log back into entries"
```

---

### Task 6: ILogReader, FileLogReader, and the crash marker

**Files:**
- Modify: `src/SharpClient.Core/Diagnostics/FileLogStore.cs`
- Create: `src/SharpClient.Core/Diagnostics/ILogReader.cs`
- Create: `src/SharpClient.Core/Diagnostics/FileLogReader.cs`
- Modify: `src/SharpClient.App/MauiProgram.cs`, `src/SharpClient.Web/Program.cs`
- Test: `tests/SharpClient.Tests/Diagnostics/FileLogReaderTests.cs`

**Interfaces:**
- Consumes: `FileLogStore` (Task 4), `LogEntry`, `CrashReport`, `LogEntryParser.Parse` (Task 5).
- Produces:
  - `FileLogStore.CrashMarkerPath` (string property).
  - `interface ILogReader { bool IsAvailable { get; } Task<IReadOnlyList<LogEntry>> ReadAsync(int maxEntries = 500); Task<CrashReport?> GetPendingCrashAsync(); Task DismissCrashAsync(); Task ClearAsync(); }`
  - `sealed class FileLogReader(FileLogStore store) : ILogReader`
  - `sealed class NoopLogReader : ILogReader`

- [ ] **Step 1: Write the failing tests**

Create `tests/SharpClient.Tests/Diagnostics/FileLogReaderTests.cs`:

```csharp
using SharpClient.Core.Diagnostics;

namespace SharpClient.Tests.Diagnostics;

public sealed class FileLogReaderTests
{
    private static (FileLogStore store, FileLogReader reader) Build()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sharpclient-tests", Guid.NewGuid().ToString("n"));
        var store = new FileLogStore(dir);
        return (store, new FileLogReader(store));
    }

    [Test]
    public async Task ReadReturnsNewestFirst()
    {
        var (store, reader) = Build();
        store.Append("Information", "App", "first");
        store.Append("Information", "App", "second");

        var entries = await reader.ReadAsync();

        await Assert.That(entries).HasCount().EqualTo(2);
        await Assert.That(entries[0].Message).IsEqualTo("second");
        await Assert.That(entries[1].Message).IsEqualTo("first");
    }

    [Test]
    public async Task ReadReturnsEmptyWhenNothingHasBeenLogged()
    {
        var (_, reader) = Build();

        await Assert.That(await reader.ReadAsync()).IsEmpty();
    }

    [Test]
    public async Task ReadMergesTheRotatedBackupBeforeTheCurrentFile()
    {
        var (store, reader) = Build();
        store.Append("Information", "App", "oldest");
        store.Append("Information", "App", new string('x', 600 * 1024));
        store.Append("Information", "App", "newest");

        var entries = await reader.ReadAsync();

        await Assert.That(entries[0].Message).IsEqualTo("newest");
        await Assert.That(entries[^1].Message).IsEqualTo("oldest");
    }

    [Test]
    public async Task MaxEntriesKeepsTheNewest()
    {
        var (store, reader) = Build();
        for (var i = 0; i < 10; i++)
        {
            store.Append("Information", "App", $"entry {i}");
        }

        var entries = await reader.ReadAsync(maxEntries: 3);

        await Assert.That(entries).HasCount().EqualTo(3);
        await Assert.That(entries[0].Message).IsEqualTo("entry 9");
        await Assert.That(entries[2].Message).IsEqualTo("entry 7");
    }

    [Test]
    public async Task NoPendingCrashOnAFreshStore()
    {
        var (_, reader) = Build();

        await Assert.That(await reader.GetPendingCrashAsync()).IsNull();
    }

    [Test]
    public async Task WriteExceptionLeavesAPendingCrash()
    {
        var (store, reader) = Build();
        store.WriteException("AppDomain", new InvalidOperationException("boom"));

        var report = await reader.GetPendingCrashAsync();

        await Assert.That(report).IsNotNull();
        await Assert.That(report!.Source).IsEqualTo("AppDomain");
        await Assert.That(report.Message).IsEqualTo("boom");
        await Assert.That(report.Detail).Contains("System.InvalidOperationException");
    }

    [Test]
    public async Task DismissClearsThePendingCrashButKeepsTheLog()
    {
        var (store, reader) = Build();
        store.WriteException("AppDomain", new InvalidOperationException("boom"));

        await reader.DismissCrashAsync();

        await Assert.That(await reader.GetPendingCrashAsync()).IsNull();
        await Assert.That(await reader.ReadAsync()).IsNotEmpty();
    }

    [Test]
    public async Task ClearDeletesTheLogFilesButNotThePendingCrash()
    {
        var (store, reader) = Build();
        store.WriteException("AppDomain", new InvalidOperationException("boom"));

        await reader.ClearAsync();

        await Assert.That(await reader.ReadAsync()).IsEmpty();
        await Assert.That(File.Exists(store.FilePath)).IsFalse();
        await Assert.That(await reader.GetPendingCrashAsync()).IsNotNull();
    }

    [Test]
    public async Task NoopReaderIsUnavailableAndEmpty()
    {
        var reader = new NoopLogReader();

        await Assert.That(reader.IsAvailable).IsFalse();
        await Assert.That(await reader.ReadAsync()).IsEmpty();
        await Assert.That(await reader.GetPendingCrashAsync()).IsNull();
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet run --project tests/SharpClient.Tests/SharpClient.Tests.csproj -c Release -- --treenode-filter "/*/*/FileLogReaderTests/*"`

Expected: build failure — `FileLogReader` does not exist.

- [ ] **Step 3: Add the crash marker to the store**

In `src/SharpClient.Core/Diagnostics/FileLogStore.cs`, add the property beside `BackupPath`:

```csharp
    /// <summary>
    /// Sidecar holding the most recent crash block. Checking one small file at launch is cheaper than
    /// scanning the log, and it survives rotation.
    /// </summary>
    public string CrashMarkerPath { get; }
```

Set it in the constructor:

```csharp
        CrashMarkerPath = Path.Combine(logDirectory, "last-crash.txt");
```

Replace `WriteException` with:

```csharp
    /// <summary>Records an unhandled exception captured by one of the global hooks.</summary>
    public void WriteException(string source, Exception? ex)
    {
        var block = FormatEntry("CRASH", source, ex?.Message ?? "(no exception object)", ex);
        Write(block);
        WriteCrashMarker(block);
    }
```

And add:

```csharp
    private void WriteCrashMarker(string block)
    {
        try
        {
            File.WriteAllText(CrashMarkerPath, block);
        }
        catch
        {
            // Same contract as the log write: recording a crash must not cause another one.
        }
    }
```

- [ ] **Step 4: Write the interface**

Create `src/SharpClient.Core/Diagnostics/ILogReader.cs`:

```csharp
namespace SharpClient.Core.Diagnostics;

/// <summary>
/// Reads the on-device diagnostics log back so the app can show it after a restart. Implemented per
/// host: the MAUI app reads the real files, the Blazor Web host has no persistent log and uses
/// <see cref="NoopLogReader"/>.
/// </summary>
public interface ILogReader
{
    /// <summary>True when a log exists on this platform and the viewer should be offered.</summary>
    public bool IsAvailable { get; }

    /// <summary>The most recent entries, newest first, across the current and rotated files.</summary>
    public Task<IReadOnlyList<LogEntry>> ReadAsync(int maxEntries = 500);

    /// <summary>The crash recorded by the previous run, or null if it exited cleanly.</summary>
    public Task<CrashReport?> GetPendingCrashAsync();

    /// <summary>Forgets the pending crash so the banner stops appearing.</summary>
    public Task DismissCrashAsync();

    /// <summary>Deletes the log and its rotated backup. Leaves any pending crash marker alone.</summary>
    public Task ClearAsync();
}

/// <summary>No-op reader for hosts without a persistent file log (e.g. the Web preview).</summary>
public sealed class NoopLogReader : ILogReader
{
    public bool IsAvailable => false;

    public Task<IReadOnlyList<LogEntry>> ReadAsync(int maxEntries = 500) =>
        Task.FromResult<IReadOnlyList<LogEntry>>([]);

    public Task<CrashReport?> GetPendingCrashAsync() => Task.FromResult<CrashReport?>(null);

    public Task DismissCrashAsync() => Task.CompletedTask;

    public Task ClearAsync() => Task.CompletedTask;
}
```

- [ ] **Step 5: Write the file reader**

Create `src/SharpClient.Core/Diagnostics/FileLogReader.cs`:

```csharp
namespace SharpClient.Core.Diagnostics;

public sealed class FileLogReader : ILogReader
{
    private readonly FileLogStore _store;

    public FileLogReader(FileLogStore store) => _store = store;

    public bool IsAvailable => true;

    public Task<IReadOnlyList<LogEntry>> ReadAsync(int maxEntries = 500)
    {
        var entries = new List<LogEntry>();
        entries.AddRange(ReadFile(_store.BackupPath));
        entries.AddRange(ReadFile(_store.FilePath));

        var start = Math.Max(0, entries.Count - maxEntries);
        var newest = entries.GetRange(start, entries.Count - start);
        newest.Reverse();

        return Task.FromResult<IReadOnlyList<LogEntry>>(newest);
    }

    public Task<CrashReport?> GetPendingCrashAsync()
    {
        var entry = ReadFile(_store.CrashMarkerPath).FirstOrDefault();
        return Task.FromResult(entry is null
            ? null
            : new CrashReport(entry.Timestamp, entry.Category, entry.Message, entry.Detail ?? string.Empty));
    }

    public Task DismissCrashAsync()
    {
        Delete(_store.CrashMarkerPath);
        return Task.CompletedTask;
    }

    public Task ClearAsync()
    {
        Delete(_store.FilePath);
        Delete(_store.BackupPath);
        return Task.CompletedTask;
    }

    private static IReadOnlyList<LogEntry> ReadFile(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return [];
            }

            // The writer may hold the file open; share aggressively rather than fail the read.
            using var stream = new FileStream(
                path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var text = new StreamReader(stream);
            return LogEntryParser.Parse(text.ReadToEnd());
        }
        catch
        {
            return [];
        }
    }

    private static void Delete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best effort: a locked file just means the viewer still shows it.
        }
    }
}
```

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet run --project tests/SharpClient.Tests/SharpClient.Tests.csproj -c Release -- --treenode-filter "/*/*/FileLogReaderTests/*"`

Expected: all 9 tests pass. Re-run the `FileLogStoreTests` filter too — `WriteException` changed.

- [ ] **Step 7: Register the reader in both hosts**

In `src/SharpClient.App/MauiProgram.cs`, beside the existing `ILogExporter` registration:

```csharp
        builder.Services.AddSingleton<ILogReader>(_ => new FileLogReader(logStore));
```

In `src/SharpClient.Web/Program.cs`, beside the existing `NoopLogExporter` registration:

```csharp
builder.Services.AddSingleton<ILogReader, NoopLogReader>();
```

- [ ] **Step 8: Full suite**

```bash
dotnet run --project tests/SharpClient.Tests/SharpClient.Tests.csproj -c Release
dotnet run --project tests/SharpClient.UI.Tests/SharpClient.UI.Tests.csproj -c Release
```

Expected: all pass.

- [ ] **Step 9: Commit**

```bash
git add src/SharpClient.Core/Diagnostics src/SharpClient.App/MauiProgram.cs src/SharpClient.Web/Program.cs tests/SharpClient.Tests/Diagnostics/FileLogReaderTests.cs
git commit -m "feat(diagnostics): ILogReader with crash marker and rotated-file merge"
```

---

### Task 7: Diagnostics viewer

**Files:**
- Create: `src/SharpClient.UI/Components/DiagnosticsView.razor`
- Create: `src/SharpClient.UI/Pages/DiagnosticsPage.razor`
- Modify: `src/SharpClient.UI/Components/SettingsView.razor`
- Modify: `src/SharpClient.UI/wwwroot/sc-interop.js`, `src/SharpClient.UI/wwwroot/app.css`
- Test: `tests/SharpClient.UI.Tests/UiFakeLogReader.cs`, `tests/SharpClient.UI.Tests/DiagnosticsViewTests.cs`

**Interfaces:**
- Consumes: `ILogReader`, `LogEntry` (Tasks 5–6), the existing `ILogExporter`.
- Produces: component `DiagnosticsView` with `[Parameter] string? InitialFilter`; route `/diagnostics` accepting `?filter=crashes`; CSS classes `.sc-diag`, `.sc-diag-bar`, `.sc-diag-chip`, `.sc-diag-chip-active`, `.sc-diag-list`, `.sc-diag-entry`, `.sc-diag-empty`, `.sc-diag-clear`; JS export `copyText(text)`; test double `UiFakeLogReader` reused by Task 8.

- [ ] **Step 1: Write the shared test double**

Create `tests/SharpClient.UI.Tests/UiFakeLogReader.cs`, matching the naming of the existing `UiFakeSession.cs` / `UiFakeWorldStore.cs`. Task 8 reuses this type, so it is not file-scoped.

```csharp
using SharpClient.Core.Diagnostics;

namespace SharpClient.UI.Tests;

public sealed class UiFakeLogReader : ILogReader
{
    public List<LogEntry> Entries { get; } = [];
    public CrashReport? Pending { get; set; }
    public int ClearCalls { get; private set; }
    public int DismissCalls { get; private set; }

    public bool IsAvailable => true;

    public Task<IReadOnlyList<LogEntry>> ReadAsync(int maxEntries = 500) =>
        Task.FromResult<IReadOnlyList<LogEntry>>(Entries);

    public Task<CrashReport?> GetPendingCrashAsync() => Task.FromResult(Pending);

    public Task DismissCrashAsync()
    {
        DismissCalls++;
        Pending = null;
        return Task.CompletedTask;
    }

    public Task ClearAsync()
    {
        ClearCalls++;
        Entries.Clear();
        return Task.CompletedTask;
    }
}
```

- [ ] **Step 2: Write the failing tests**

Create `tests/SharpClient.UI.Tests/DiagnosticsViewTests.cs`:

```csharp
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using SharpClient.Core.Diagnostics;
using SharpClient.UI.Components;

namespace SharpClient.UI.Tests;

public sealed class DiagnosticsViewTests
{
    private static readonly DateTimeOffset Base = new(2026, 8, 10, 23, 41, 0, TimeSpan.Zero);

    private static (BunitContext ctx, UiFakeLogReader reader) NewContext()
    {
        var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        var reader = new UiFakeLogReader();
        ctx.Services.AddSingleton<ILogReader>(reader);
        ctx.Services.AddSingleton<ILogExporter>(new NoopLogExporter());
        return (ctx, reader);
    }

    private static void Seed(UiFakeLogReader reader)
    {
        reader.Entries.Add(new LogEntry(Base.AddSeconds(2), "CRASH", "AppDomain", "boom", "stack frame here"));
        reader.Entries.Add(new LogEntry(Base.AddSeconds(1), "Error", "Session", "send failed", null));
        reader.Entries.Add(new LogEntry(Base, "Information", "App", "started", null));
    }

    [Test]
    public async Task RendersEveryEntryByDefault()
    {
        var (ctx, reader) = NewContext();
        using var _ = ctx;
        Seed(reader);

        var cut = ctx.Render<DiagnosticsView>();

        await Assert.That(cut.FindAll(".sc-diag-entry")).HasCount().EqualTo(3);
    }

    [Test]
    public async Task CrashesFilterShowsOnlyCrashEntries()
    {
        var (ctx, reader) = NewContext();
        using var _ = ctx;
        Seed(reader);

        var cut = ctx.Render<DiagnosticsView>();
        await cut.FindAll(".sc-diag-chip")[2].ClickAsync(new MouseEventArgs());

        await Assert.That(cut.FindAll(".sc-diag-entry")).HasCount().EqualTo(1);
        await Assert.That(cut.Find(".sc-diag-entry").TextContent).Contains("boom");
    }

    [Test]
    public async Task ErrorsFilterIncludesErrorsAndCrashes()
    {
        var (ctx, reader) = NewContext();
        using var _ = ctx;
        Seed(reader);

        var cut = ctx.Render<DiagnosticsView>();
        await cut.FindAll(".sc-diag-chip")[1].ClickAsync(new MouseEventArgs());

        await Assert.That(cut.FindAll(".sc-diag-entry")).HasCount().EqualTo(2);
    }

    [Test]
    public async Task InitialFilterParameterSelectsCrashes()
    {
        var (ctx, reader) = NewContext();
        using var _ = ctx;
        Seed(reader);

        var cut = ctx.Render<DiagnosticsView>(p => p.Add(c => c.InitialFilter, "crashes"));

        await Assert.That(cut.FindAll(".sc-diag-entry")).HasCount().EqualTo(1);
    }

    [Test]
    public async Task DetailIsRenderedForEntriesThatHaveIt()
    {
        var (ctx, reader) = NewContext();
        using var _ = ctx;
        Seed(reader);

        var cut = ctx.Render<DiagnosticsView>();

        await Assert.That(cut.FindAll("details")).HasCount().EqualTo(1);
        await Assert.That(cut.Find("details").TextContent).Contains("stack frame here");
    }

    [Test]
    public async Task EmptyStateIsShownWhenThereAreNoEntries()
    {
        var (ctx, _) = NewContext();
        using var __ = ctx;

        var cut = ctx.Render<DiagnosticsView>();

        await Assert.That(cut.FindAll(".sc-diag-empty")).HasCount().EqualTo(1);
    }

    [Test]
    public async Task ClearRequiresASecondConfirmingClick()
    {
        var (ctx, reader) = NewContext();
        using var _ = ctx;
        Seed(reader);

        var cut = ctx.Render<DiagnosticsView>();
        await cut.Find(".sc-diag-clear").ClickAsync(new MouseEventArgs());

        await Assert.That(reader.ClearCalls).IsEqualTo(0);
        await Assert.That(cut.Find(".sc-diag-clear").TextContent.Trim()).IsEqualTo("Confirm clear");

        await cut.Find(".sc-diag-clear").ClickAsync(new MouseEventArgs());

        await Assert.That(reader.ClearCalls).IsEqualTo(1);
        await Assert.That(cut.FindAll(".sc-diag-entry")).IsEmpty();
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet run --project tests/SharpClient.UI.Tests/SharpClient.UI.Tests.csproj -c Release -- --treenode-filter "/*/*/DiagnosticsViewTests/*"`

Expected: build failure — `DiagnosticsView` does not exist.

- [ ] **Step 4: Add the clipboard interop export**

Append to `src/SharpClient.UI/wwwroot/sc-interop.js`:

```js
export function copyText(text) {
    if (navigator.clipboard && navigator.clipboard.writeText) {
        return navigator.clipboard.writeText(text);
    }
    return Promise.resolve();
}
```

- [ ] **Step 5: Write the component**

Create `src/SharpClient.UI/Components/DiagnosticsView.razor`:

```razor
@using Microsoft.JSInterop
@using SharpClient.Core.Diagnostics
@inject ILogReader Reader
@inject ILogExporter Exporter
@inject IJSRuntime JS
@implements IAsyncDisposable

<div class="sc-diag">
    <div class="sc-diag-bar">
        @foreach (var option in FilterOptions)
        {
            var value = option.Value;
            <button type="button"
                    class="sc-diag-chip @(_filter == value ? "sc-diag-chip-active" : "")"
                    @onclick="() => _filter = value">@option.Label</button>
        }
        <div class="sc-diag-actions">
            <button type="button" class="sc-rules-btn sc-diag-refresh" @onclick="RefreshAsync">Refresh</button>
            <button type="button" class="sc-rules-btn sc-diag-copy" @onclick="CopyAsync">Copy</button>
            <button type="button" class="sc-rules-btn sc-diag-share" @onclick="ShareAsync">Share</button>
            <button type="button" class="sc-rules-btn sc-diag-clear" @onclick="ClearAsync">
                @(_confirmingClear ? "Confirm clear" : "Clear")
            </button>
        </div>
    </div>

    <div class="sc-diag-list">
        @if (Visible.Count == 0)
        {
            <div class="sc-diag-empty">No log entries recorded yet.</div>
        }
        else
        {
            @foreach (var entry in Visible)
            {
                <div class="sc-diag-entry">
                    <div class="sc-diag-entry-head">
                        <span class="sc-diag-time">@entry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss")</span>
                        <span class="sc-diag-level sc-diag-level-@entry.Level.ToLowerInvariant()">@entry.Level</span>
                        @if (entry.Category.Length > 0)
                        {
                            <span class="sc-diag-category">@entry.Category</span>
                        }
                    </div>
                    <div class="sc-diag-message">@entry.Message</div>
                    @if (entry.Detail is not null)
                    {
                        <details class="sc-diag-detail">
                            <summary>Detail</summary>
                            <pre>@entry.Detail</pre>
                        </details>
                    }
                </div>
            }
            @if (_truncated)
            {
                <div class="sc-diag-empty">Older entries were truncated.</div>
            }
        }
    </div>
</div>

@code {
    private const int MaxEntries = 500;

    [Parameter]
    public string? InitialFilter { get; set; }

    private enum LogFilter { All, Errors, Crashes }

    private static readonly (string Label, LogFilter Value)[] FilterOptions =
    [
        ("All", LogFilter.All),
        ("Errors", LogFilter.Errors),
        ("Crashes", LogFilter.Crashes),
    ];

    private IReadOnlyList<LogEntry> _entries = [];
    private LogFilter _filter = LogFilter.All;
    private bool _confirmingClear;
    private bool _truncated;
    private IJSObjectReference? _interop;

    private List<LogEntry> Visible => _filter switch
    {
        LogFilter.Crashes => _entries.Where(IsCrash).ToList(),
        LogFilter.Errors => _entries.Where(e => IsCrash(e) || IsError(e)).ToList(),
        _ => _entries.ToList(),
    };

    private static bool IsCrash(LogEntry e) => e.Level.Equals("CRASH", StringComparison.OrdinalIgnoreCase);

    private static bool IsError(LogEntry e) =>
        e.Level.Equals("Error", StringComparison.OrdinalIgnoreCase)
        || e.Level.Equals("Critical", StringComparison.OrdinalIgnoreCase);

    protected override async Task OnInitializedAsync()
    {
        if (string.Equals(InitialFilter, "crashes", StringComparison.OrdinalIgnoreCase))
        {
            _filter = LogFilter.Crashes;
        }
        else if (string.Equals(InitialFilter, "errors", StringComparison.OrdinalIgnoreCase))
        {
            _filter = LogFilter.Errors;
        }

        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        _entries = await Reader.ReadAsync(MaxEntries);
        _truncated = _entries.Count == MaxEntries;
        _confirmingClear = false;
    }

    private async Task CopyAsync()
    {
        var text = string.Join('\n', Visible.Select(e =>
            $"{e.Timestamp:yyyy-MM-dd HH:mm:ss} [{e.Level}] {e.Category}: {e.Message}"
            + (e.Detail is null ? string.Empty : "\n" + e.Detail)));

        _interop ??= await JS.InvokeAsync<IJSObjectReference>(
            "import", "./_content/SharpClient.UI/sc-interop.js");
        await _interop.InvokeVoidAsync("copyText", text);
    }

    private async Task ShareAsync()
    {
        if (Exporter.IsAvailable)
        {
            await Exporter.ShareAsync();
        }
    }

    // Two-step rather than a JS confirm(): a modal dialog inside the Android WebView blocks the
    // Blazor circuit.
    private async Task ClearAsync()
    {
        if (!_confirmingClear)
        {
            _confirmingClear = true;
            return;
        }

        await Reader.ClearAsync();
        await RefreshAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_interop is not null)
        {
            await _interop.DisposeAsync();
        }
    }
}
```

- [ ] **Step 6: Write the page**

Create `src/SharpClient.UI/Pages/DiagnosticsPage.razor`:

```razor
@page "/diagnostics"

<PageTitle>SharpClient &middot; Diagnostics</PageTitle>

<DiagnosticsView InitialFilter="@Filter" />

@code {
    [Parameter]
    [SupplyParameterFromQuery(Name = "filter")]
    public string? Filter { get; set; }
}
```

- [ ] **Step 7: Link it from Settings**

In `src/SharpClient.UI/Components/SettingsView.razor`, inside the Diagnostics section, add a row above the existing export row:

```razor
            <div class="sc-setting">
                <span class="sc-setting-label">View log</span>
                <a class="sc-rules-btn" href="/diagnostics">Open</a>
            </div>
```

- [ ] **Step 8: Add the styles**

Append to `src/SharpClient.UI/wwwroot/app.css`:

```css
/* ── Diagnostics page ──────────────────────────────────────────── */
.sc-diag { height: 100%; display: flex; flex-direction: column; gap: 10px; padding: 12px 12px 0; }
.sc-diag-bar { display: flex; flex-wrap: wrap; align-items: center; gap: 6px; }
.sc-diag-chip { font-family: var(--mono); font-size: 12px; color: var(--dim); background: var(--panel); border: 1px solid var(--bd); border-radius: 8px; padding: 6px 11px; cursor: pointer; }
.sc-diag-chip-active { color: var(--acc2); background: var(--acc-soft); border-color: var(--acc-line); }
.sc-diag-actions { margin-left: auto; display: flex; flex-wrap: wrap; gap: 6px; }
.sc-diag-list { flex: 1; min-height: 0; overflow-y: auto; display: flex; flex-direction: column; gap: 8px; padding-bottom: 12px; }
.sc-diag-entry { background: var(--panel); border: 1px solid var(--bd); border-radius: 10px; padding: 9px 11px; }
.sc-diag-entry-head { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; margin-bottom: 4px; }
.sc-diag-time { font-family: var(--mono); font-size: 11px; color: var(--faint); }
.sc-diag-level { font-family: var(--mono); font-size: 10px; font-weight: 600; text-transform: uppercase; letter-spacing: .06em; color: var(--dim); border: 1px solid var(--bd2); border-radius: 6px; padding: 1px 6px; }
.sc-diag-level-error, .sc-diag-level-critical, .sc-diag-level-crash { color: #e88; border-color: rgba(238,136,136,.45); }
.sc-diag-category { font-family: var(--mono); font-size: 11px; color: var(--dim); }
.sc-diag-message { font-family: var(--mono); font-size: 12px; color: var(--tx); overflow-wrap: anywhere; }
.sc-diag-detail { margin-top: 6px; }
.sc-diag-detail summary { font-family: var(--mono); font-size: 11px; color: var(--dim); cursor: pointer; }
.sc-diag-detail pre { margin-top: 6px; font-family: var(--mono); font-size: 11px; color: var(--dim); white-space: pre-wrap; overflow-wrap: anywhere; }
.sc-diag-empty { font-family: var(--mono); font-size: 12px; color: var(--faint); padding: 14px 2px; }
```

- [ ] **Step 9: Run the tests to verify they pass**

Run: `dotnet run --project tests/SharpClient.UI.Tests/SharpClient.UI.Tests.csproj -c Release -- --treenode-filter "/*/*/DiagnosticsViewTests/*"`

Expected: all 7 tests pass.

- [ ] **Step 10: Confirm SettingsView tests still pass**

Run: `dotnet run --project tests/SharpClient.UI.Tests/SharpClient.UI.Tests.csproj -c Release -- --treenode-filter "/*/*/SettingsViewTests/*"`

Expected: all pass. Those tests register `NoopLogExporter`, whose `IsAvailable` is false, so the whole Diagnostics section including the new row stays hidden.

- [ ] **Step 11: Commit**

```bash
git add src/SharpClient.UI/Components/DiagnosticsView.razor src/SharpClient.UI/Pages/DiagnosticsPage.razor src/SharpClient.UI/Components/SettingsView.razor src/SharpClient.UI/wwwroot/sc-interop.js src/SharpClient.UI/wwwroot/app.css tests/SharpClient.UI.Tests/DiagnosticsViewTests.cs
git commit -m "feat(diagnostics): in-app log viewer with level filters"
```

---

### Task 8: Crash banner

**Files:**
- Create: `src/SharpClient.UI/Components/CrashBanner.razor`
- Modify: `src/SharpClient.UI/Layout/MainLayout.razor`, `src/SharpClient.UI/wwwroot/app.css`
- Test: `tests/SharpClient.UI.Tests/CrashBannerTests.cs`

**Interfaces:**
- Consumes: `ILogReader.GetPendingCrashAsync()`, `ILogReader.DismissCrashAsync()`, `CrashReport` (Task 6), `UiFakeLogReader` (Task 7).
- Produces: component `CrashBanner` (no parameters); CSS classes `.sc-crash-banner`, `.sc-crash-view`, `.sc-crash-dismiss`.

- [ ] **Step 1: Write the failing tests**

Create `tests/SharpClient.UI.Tests/CrashBannerTests.cs`, reusing the `UiFakeLogReader` that Task 7 added — do not define a second `ILogReader` double.

```csharp
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using SharpClient.Core.Diagnostics;
using SharpClient.UI.Components;

namespace SharpClient.UI.Tests;

public sealed class CrashBannerTests
{
    private static (BunitContext ctx, UiFakeLogReader reader) NewContext(CrashReport? pending)
    {
        var ctx = new BunitContext();
        var reader = new UiFakeLogReader { Pending = pending };
        ctx.Services.AddSingleton<ILogReader>(reader);
        return (ctx, reader);
    }

    [Test]
    public async Task NothingIsRenderedWhenThereIsNoPendingCrash()
    {
        var (ctx, _) = NewContext(null);
        using var _unused = ctx;

        var cut = ctx.Render<CrashBanner>();

        await Assert.That(cut.FindAll(".sc-crash-banner")).IsEmpty();
    }

    [Test]
    public async Task BannerShowsTheCrashTimestamp()
    {
        var report = new CrashReport(
            new DateTimeOffset(2026, 8, 9, 23, 41, 0, TimeSpan.Zero), "AppDomain", "boom", "stack");
        var (ctx, _) = NewContext(report);
        using var _unused = ctx;

        var cut = ctx.Render<CrashBanner>();

        await Assert.That(cut.FindAll(".sc-crash-banner")).HasCount().EqualTo(1);
        await Assert.That(cut.Find(".sc-crash-banner").TextContent).Contains("2026-08-09 23:41");
    }

    [Test]
    public async Task DismissingHidesTheBannerAndTellsTheReader()
    {
        var report = new CrashReport(
            new DateTimeOffset(2026, 8, 9, 23, 41, 0, TimeSpan.Zero), "AppDomain", "boom", "stack");
        var (ctx, reader) = NewContext(report);
        using var _unused = ctx;

        var cut = ctx.Render<CrashBanner>();
        await cut.Find(".sc-crash-dismiss").ClickAsync(new MouseEventArgs());

        await Assert.That(reader.DismissCalls).IsEqualTo(1);
        await Assert.That(cut.FindAll(".sc-crash-banner")).IsEmpty();
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet run --project tests/SharpClient.UI.Tests/SharpClient.UI.Tests.csproj -c Release -- --treenode-filter "/*/*/CrashBannerTests/*"`

Expected: build failure — `CrashBanner` does not exist.

- [ ] **Step 3: Write the component**

Create `src/SharpClient.UI/Components/CrashBanner.razor`. The report is loaded in `OnInitializedAsync` so bUnit's synchronous `Render` sees it; the file read is small and guarded inside `FileLogReader`.

```razor
@using SharpClient.Core.Diagnostics
@inject ILogReader Reader
@inject NavigationManager Nav

@if (_report is not null)
{
    <div class="sc-crash-banner" role="status">
        <span class="sc-crash-text">
            SharpClient crashed last run (@_report.Timestamp.ToString("yyyy-MM-dd HH:mm"))
        </span>
        <button type="button" class="sc-crash-view" @onclick="View">View</button>
        <button type="button" class="sc-crash-dismiss" aria-label="Dismiss" @onclick="DismissAsync">✕</button>
    </div>
}

@code {
    private CrashReport? _report;

    protected override async Task OnInitializedAsync() => _report = await Reader.GetPendingCrashAsync();

    private void View() => Nav.NavigateTo("/diagnostics?filter=crashes");

    private async Task DismissAsync()
    {
        await Reader.DismissCrashAsync();
        _report = null;
    }
}
```

`NavigationManager` resolves from `Microsoft.AspNetCore.Components`, already imported by the Razor SDK's implicit usings.

- [ ] **Step 4: Mount it in the layout**

In `src/SharpClient.UI/Layout/MainLayout.razor`, add immediately before `<div class="sc-content">`:

```razor
    <CrashBanner />
```

- [ ] **Step 5: Add the styles**

Append to `src/SharpClient.UI/wwwroot/app.css`:

```css
/* ── Crash banner ──────────────────────────────────────────────── */
.sc-crash-banner { display: flex; align-items: center; gap: 10px; padding: 9px 12px; background: rgba(238,136,136,.12); border-bottom: 1px solid rgba(238,136,136,.4); }
.sc-crash-text { flex: 1; font-family: var(--mono); font-size: 11px; color: #e8a; overflow-wrap: anywhere; }
.sc-crash-view { font-family: var(--mono); font-size: 11px; color: var(--acc2); background: var(--acc-soft); border: 1px solid var(--acc-line); border-radius: 7px; padding: 4px 10px; cursor: pointer; }
.sc-crash-dismiss { font-size: 12px; line-height: 1; color: var(--dim); background: none; border: none; padding: 4px 6px; cursor: pointer; }
```

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet run --project tests/SharpClient.UI.Tests/SharpClient.UI.Tests.csproj -c Release -- --treenode-filter "/*/*/CrashBannerTests/*"`

Expected: all 3 tests pass.

- [ ] **Step 7: Run every suite plus the Android head**

```bash
dotnet run --project tests/SharpClient.Tests/SharpClient.Tests.csproj -c Release
dotnet run --project tests/SharpClient.UI.Tests/SharpClient.UI.Tests.csproj -c Release
dotnet run --project tests/SharpClient.Data.Tests/SharpClient.Data.Tests.csproj -c Release
dotnet build src/SharpClient.App/SharpClient.App.csproj -f net10.0-android -c Release
dotnet build src/SharpClient.Web/SharpClient.Web.csproj -c Release
```

Expected: all green. Report explicitly if the Android head could not be built locally for want of the workload.

- [ ] **Step 8: Commit**

```bash
git add src/SharpClient.UI/Components/CrashBanner.razor src/SharpClient.UI/Layout/MainLayout.razor src/SharpClient.UI/wwwroot/app.css tests/SharpClient.UI.Tests/CrashBannerTests.cs
git commit -m "feat(diagnostics): crashed-last-run banner linking to the log viewer"
```

---

## Manual verification

After Task 8, run the Web host (`dotnet run --project src/SharpClient.Web/SharpClient.Web.csproj`) and check:

1. The Compose tab appears in the nav and its editor fills the body between the chip row and the footer, at both phone and desktop widths.
2. With no connected session, Send is disabled and the hint links to `/session`.
3. Preview shows the exact wire line; Edit returns to the draft unchanged.
4. Settings shows no Diagnostics section — the Web host uses `NoopLogExporter`/`NoopLogReader` and no crash banner appears.

The MAUI-only paths (real log file, share sheet, crash banner) need an Android or Windows run of `src/SharpClient.App`. Force a crash by throwing from a component event handler, kill and relaunch the app, and confirm the banner appears and the entry is visible under the Crashes filter.
