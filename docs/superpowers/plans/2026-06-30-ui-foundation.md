# SharpClient UI Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the interface-based foundation for SharpClient's UI: extract Core service interfaces, add the design's ANSI palette + `StyledSegment`→CSS render contract, a session/tab manager and presentation view models, and the first Blazor components — all unit-tested without an Android device (TUnit for logic, bUnit for components).

**Architecture:** Phase 1 concretes (`TelnetConnection`, `Session`) are put behind interfaces (`ITelnetConnection`, `ISession`) so collaborators are faked in tests. Pure rendering logic (`AnsiPalette`, `SegmentStyle`) maps the design tokens to CSS. `SessionManager` (behind `ISessionManager`) owns the open tabs; `SessionsViewModel` holds the session-screen presentation logic against those interfaces. Razor components in the `SharpClient.UI` RCL bind to the view models and are rendered/asserted with bUnit from C# tests.

**Tech Stack:** .NET 10 (`net10.0`), TUnit 1.57, bUnit 2.7.2 (C#-based tests — TUnit + Razor source generators cannot share `.razor` test files), Microsoft.AspNetCore.Components.

## Global Constraints

- Target framework `net10.0` for all non-MAUI projects. `TreatWarningsAsErrors=true` solution-wide (`Directory.Build.props`) — every task builds at **0 warnings**.
- Nullable reference types + implicit usings enabled globally — never redeclare in csproj.
- File-scoped namespaces (enforced as warning → error by `.editorconfig`).
- Test framework is **TUnit**, not xUnit: `[Test]`, `[Arguments(...)]`, `await Assert.That(actual).IsEqualTo(expected)` / `.IsTrue()` / `.IsFalse()` / `.IsNotNull()`.
- **Interface-based testing:** depend on interfaces, replace collaborators with hand-written fakes (no mocking framework).
- bUnit tests are **C# files** using `Bunit.BunitContext` directly (bUnit 2.x; framework-agnostic, instantiated per test). Components under test stay `.razor` in `SharpClient.UI`.
- ANSI palette hexes and the render contract are defined verbatim in `docs/design/design-tokens.md` — use those exact values.
- Run logic suite: `dotnet run --project tests/SharpClient.Tests/SharpClient.Tests.csproj`. Run UI suite: `dotnet run --project tests/SharpClient.UI.Tests/SharpClient.UI.Tests.csproj`.
- All paths are relative to `/home/grave/RiderProjects/SharpClient`.

---

## File Structure

- `src/SharpClient.Core/Connection/ConnectionState.cs` — **modify**: add `Reconnecting`, `Error`.
- `src/SharpClient.Core/Connection/ITelnetConnection.cs` — **create**: interface over `TelnetConnection`.
- `src/SharpClient.Core/Connection/TelnetConnection.cs` — **modify**: implement `ITelnetConnection`.
- `src/SharpClient.Core/Sessions/ISession.cs` — **create**: interface over `Session`.
- `src/SharpClient.Core/Sessions/Session.cs` — **modify**: implement `ISession`, take `ITelnetConnection`, add `StateChanged`.
- `src/SharpClient.Core/Rendering/AnsiPalette.cs` — **create**: index 0–255 → hex.
- `src/SharpClient.Core/Rendering/SegmentStyle.cs` — **create**: `StyledSegment` → CSS string.
- `src/SharpClient.Core/Sessions/ISessionManager.cs` + `SessionManager.cs` — **create**: open tabs + active.
- `src/SharpClient.Core/Presentation/SessionsViewModel.cs` — **create**: session-screen presentation logic.
- `tests/SharpClient.Tests/...` — TUnit tests + fakes (`FakeTelnetConnection`, `FakeSession`, `FakeSessionManager`).
- `src/SharpClient.UI/Components/OutputView.razor` — **create**: renders scrollback → spans.
- `tests/SharpClient.UI.Tests/` — **create**: bUnit C# tests for components.

---

## Task 1: Extend ConnectionState + extract ITelnetConnection / ISession

**Files:**
- Modify: `src/SharpClient.Core/Connection/ConnectionState.cs`
- Create: `src/SharpClient.Core/Connection/ITelnetConnection.cs`
- Modify: `src/SharpClient.Core/Connection/TelnetConnection.cs`
- Create: `src/SharpClient.Core/Sessions/ISession.cs`
- Modify: `src/SharpClient.Core/Sessions/Session.cs`
- Create: `tests/SharpClient.Tests/Sessions/FakeTelnetConnection.cs`
- Test: `tests/SharpClient.Tests/Sessions/SessionStateTests.cs`

**Interfaces:**
- Consumes: Phase 1 `TelnetConnection`, `Session`, `ScrollbackLine`, `ConnectionState`.
- Produces:
  - `enum ConnectionState { Disconnected, Connecting, Connected, Reconnecting, Error }`
  - `interface ITelnetConnection : IAsyncDisposable` with `event Action<string>? LineReceived; event Action<ConnectionState>? StateChanged; ConnectionState State { get; } Task ConnectAsync(string host, int port, CancellationToken ct = default); Task SendAsync(string line); Task DisconnectAsync();`
  - `interface ISession : IAsyncDisposable` with `IReadOnlyList<ScrollbackLine> Scrollback { get; } event Action<ScrollbackLine>? LineAppended; event Action<ConnectionState>? StateChanged; ConnectionState State { get; } Task ConnectAsync(string host, int port, CancellationToken ct = default); Task SendAsync(string line);`
  - `Session(ITelnetConnection connection)` now takes the interface; `Session.StateChanged` forwards the connection's `StateChanged`.

- [ ] **Step 1: Write the failing test**

Create `tests/SharpClient.Tests/Sessions/FakeTelnetConnection.cs`:

```csharp
using SharpClient.Core.Connection;

namespace SharpClient.Tests.Sessions;

public sealed class FakeTelnetConnection : ITelnetConnection
{
    public event Action<string>? LineReceived;
    public event Action<ConnectionState>? StateChanged;

    public ConnectionState State { get; private set; } = ConnectionState.Disconnected;
    public List<string> Sent { get; } = [];

    public Task ConnectAsync(string host, int port, CancellationToken cancellationToken = default)
    {
        RaiseState(ConnectionState.Connected);
        return Task.CompletedTask;
    }

    public Task SendAsync(string line)
    {
        Sent.Add(line);
        return Task.CompletedTask;
    }

    public Task DisconnectAsync()
    {
        RaiseState(ConnectionState.Disconnected);
        return Task.CompletedTask;
    }

    public void Emit(string line) => LineReceived?.Invoke(line);

    public void RaiseState(ConnectionState state)
    {
        State = state;
        StateChanged?.Invoke(state);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
```

Create `tests/SharpClient.Tests/Sessions/SessionStateTests.cs`:

```csharp
using SharpClient.Core.Connection;
using SharpClient.Core.Sessions;

namespace SharpClient.Tests.Sessions;

public sealed class SessionStateTests
{
    [Test]
    public async Task SessionForwardsConnectionStateChanges()
    {
        var fake = new FakeTelnetConnection();
        await using var session = new Session(fake);

        var states = new List<ConnectionState>();
        session.StateChanged += states.Add;

        fake.RaiseState(ConnectionState.Connecting);
        fake.RaiseState(ConnectionState.Error);

        await Assert.That(states).IsEquivalentTo(new[] { ConnectionState.Connecting, ConnectionState.Error });
        await Assert.That(session.State).IsEqualTo(ConnectionState.Error);
    }

    [Test]
    public async Task SessionParsesLinesFromAnyConnection()
    {
        var fake = new FakeTelnetConnection();
        await using var session = new Session(fake);

        fake.Emit("plain");

        await Assert.That(session.Scrollback.Count).IsEqualTo(1);
        await Assert.That(session.Scrollback[0].Segments[0].Text).IsEqualTo("plain");
    }

    [Test]
    public async Task ReconnectingAndErrorAreDistinctStates()
    {
        await Assert.That(ConnectionState.Reconnecting).IsNotEqualTo(ConnectionState.Error);
        await Assert.That((int)ConnectionState.Error).IsEqualTo(4);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet run --project tests/SharpClient.Tests/SharpClient.Tests.csproj`
Expected: FAIL — `ITelnetConnection` does not exist; `Session` does not take it; `Session.StateChanged` missing; `ConnectionState.Reconnecting/Error` missing.

- [ ] **Step 3: Write minimal implementation**

Replace `src/SharpClient.Core/Connection/ConnectionState.cs`:

```csharp
namespace SharpClient.Core.Connection;

public enum ConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Reconnecting,
    Error,
}
```

Create `src/SharpClient.Core/Connection/ITelnetConnection.cs`:

```csharp
namespace SharpClient.Core.Connection;

public interface ITelnetConnection : IAsyncDisposable
{
    event Action<string>? LineReceived;

    event Action<ConnectionState>? StateChanged;

    ConnectionState State { get; }

    Task ConnectAsync(string host, int port, CancellationToken cancellationToken = default);

    Task SendAsync(string line);

    Task DisconnectAsync();
}
```

Modify `src/SharpClient.Core/Connection/TelnetConnection.cs`: change the class declaration so it implements the interface (members already match):

```csharp
public sealed class TelnetConnection(ITelnetInterpreterFactory factory) : ITelnetConnection
```

(Remove the now-redundant `: IAsyncDisposable` — `ITelnetConnection` already extends it. Keep everything else.)

Create `src/SharpClient.Core/Sessions/ISession.cs`:

```csharp
using SharpClient.Core.Connection;

namespace SharpClient.Core.Sessions;

public interface ISession : IAsyncDisposable
{
    IReadOnlyList<ScrollbackLine> Scrollback { get; }

    event Action<ScrollbackLine>? LineAppended;

    event Action<ConnectionState>? StateChanged;

    ConnectionState State { get; }

    Task ConnectAsync(string host, int port, CancellationToken cancellationToken = default);

    Task SendAsync(string line);
}
```

Modify `src/SharpClient.Core/Sessions/Session.cs` to implement `ISession`, take `ITelnetConnection`, and forward state. Full new file:

```csharp
using SharpClient.Core.Connection;
using SharpClient.Core.Rendering;

namespace SharpClient.Core.Sessions;

public sealed record ScrollbackLine(IReadOnlyList<StyledSegment> Segments);

public sealed class Session : ISession
{
    private readonly ITelnetConnection _connection;
    private readonly List<ScrollbackLine> _scrollback = [];

    public Session(ITelnetConnection connection)
    {
        _connection = connection;
        _connection.LineReceived += OnLineReceived;
        _connection.StateChanged += OnStateChanged;
    }

    public IReadOnlyList<ScrollbackLine> Scrollback => _scrollback;

    public event Action<ScrollbackLine>? LineAppended;

    public event Action<ConnectionState>? StateChanged;

    public ConnectionState State => _connection.State;

    public Task ConnectAsync(string host, int port, CancellationToken cancellationToken = default) =>
        _connection.ConnectAsync(host, port, cancellationToken);

    public Task SendAsync(string line) => _connection.SendAsync(line);

    public async ValueTask DisposeAsync()
    {
        _connection.LineReceived -= OnLineReceived;
        _connection.StateChanged -= OnStateChanged;
        await _connection.DisposeAsync();
    }

    private void OnLineReceived(string raw)
    {
        var line = new ScrollbackLine(AnsiParser.Parse(raw));
        _scrollback.Add(line);
        LineAppended?.Invoke(line);
    }

    private void OnStateChanged(ConnectionState state) => StateChanged?.Invoke(state);
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet run --project tests/SharpClient.Tests/SharpClient.Tests.csproj`
Expected: PASS — all prior tests plus the 3 new ones. The existing `SessionTests` still compiles because `TelnetConnection` implements `ITelnetConnection`.

- [ ] **Step 5: Commit**

```bash
git add src/SharpClient.Core tests/SharpClient.Tests/Sessions
git commit -m "feat(core): extract ITelnetConnection/ISession, add Reconnecting/Error + Session.StateChanged"
```

---

## Task 2: AnsiPalette

**Files:**
- Create: `src/SharpClient.Core/Rendering/AnsiPalette.cs`
- Test: `tests/SharpClient.Tests/Rendering/AnsiPaletteTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `static class AnsiPalette` with `static string ToHex(int index)` — index 0–255 → `#rrggbb`. Indices 0–15 are the design's tuned palette; 16–231 the 6×6×6 cube; 232–255 the grayscale ramp. Out-of-range returns the phosphor default `#c4d1c8`.

- [ ] **Step 1: Write the failing test**

Create `tests/SharpClient.Tests/Rendering/AnsiPaletteTests.cs`:

```csharp
using SharpClient.Core.Rendering;

namespace SharpClient.Tests.Rendering;

public sealed class AnsiPaletteTests
{
    [Test]
    [Arguments(0, "#3a3f4b")]
    [Arguments(1, "#e06c75")]
    [Arguments(7, "#abb2bf")]
    [Arguments(8, "#5c6672")]
    [Arguments(15, "#e8edf2")]
    public async Task BaseSixteenMatchDesignTokens(int index, string hex)
    {
        await Assert.That(AnsiPalette.ToHex(index)).IsEqualTo(hex);
    }

    [Test]
    public async Task CubeBlackIsIndex16()
    {
        await Assert.That(AnsiPalette.ToHex(16)).IsEqualTo("#000000");
    }

    [Test]
    public async Task CubeWhiteIsIndex231()
    {
        await Assert.That(AnsiPalette.ToHex(231)).IsEqualTo("#ffffff");
    }

    [Test]
    public async Task GrayscaleRampStartsDark()
    {
        await Assert.That(AnsiPalette.ToHex(232)).IsEqualTo("#080808");
    }

    [Test]
    public async Task OutOfRangeFallsBackToPhosphor()
    {
        await Assert.That(AnsiPalette.ToHex(999)).IsEqualTo("#c4d1c8");
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet run --project tests/SharpClient.Tests/SharpClient.Tests.csproj`
Expected: FAIL — `AnsiPalette` does not exist.

- [ ] **Step 3: Write minimal implementation**

Create `src/SharpClient.Core/Rendering/AnsiPalette.cs`:

```csharp
namespace SharpClient.Core.Rendering;

public static class AnsiPalette
{
    private const string PhosphorDefault = "#c4d1c8";

    private static readonly string[] Base16 =
    [
        "#3a3f4b", "#e06c75", "#8fc16f", "#e5c07b", "#61afef", "#c678dd", "#56b6c2", "#abb2bf",
        "#5c6672", "#ff8088", "#b5e890", "#ffd596", "#7cc4ff", "#e29bf0", "#6fd3df", "#e8edf2",
    ];

    private static readonly int[] CubeSteps = [0, 95, 135, 175, 215, 255];

    public static string ToHex(int index)
    {
        if (index is >= 0 and < 16)
        {
            return Base16[index];
        }

        if (index is >= 16 and <= 231)
        {
            var n = index - 16;
            var r = CubeSteps[n / 36 % 6];
            var g = CubeSteps[n / 6 % 6];
            var b = CubeSteps[n % 6];
            return Rgb(r, g, b);
        }

        if (index is >= 232 and <= 255)
        {
            var v = 8 + (index - 232) * 10;
            return Rgb(v, v, v);
        }

        return PhosphorDefault;
    }

    private static string Rgb(int r, int g, int b) =>
        $"#{r:x2}{g:x2}{b:x2}";
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet run --project tests/SharpClient.Tests/SharpClient.Tests.csproj`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/SharpClient.Core/Rendering/AnsiPalette.cs tests/SharpClient.Tests/Rendering/AnsiPaletteTests.cs
git commit -m "feat(core): add AnsiPalette (design 16-colour + xterm-256 cube/grayscale)"
```

---

## Task 3: SegmentStyle render mapper

**Files:**
- Create: `src/SharpClient.Core/Rendering/SegmentStyle.cs`
- Test: `tests/SharpClient.Tests/Rendering/SegmentStyleTests.cs`

**Interfaces:**
- Consumes: `StyledSegment`, `TextStyle`, `AnsiColor`, `AnsiColorKind` (Task 1 of Phase 1), `AnsiPalette` (Task 2).
- Produces: `static class SegmentStyle` with `static string ToCss(StyledSegment segment)` returning an inline CSS string for the span's `style` attribute, per the render contract: default fg → `#c4d1c8`; indexed fg/bg → palette hex; indexed bg adds `padding:0 2px`; inverse swaps fg/bg (fg becomes `#090c10`, bg becomes the segment's fg colour or phosphor default); bold → `font-weight:700`; underline → `text-decoration:underline`. Declarations are emitted in a stable order: `color`, `background`, `padding`, `font-weight`, `text-decoration`, each terminated with `;`.

- [ ] **Step 1: Write the failing test**

Create `tests/SharpClient.Tests/Rendering/SegmentStyleTests.cs`:

```csharp
using SharpClient.Core.Rendering;

namespace SharpClient.Tests.Rendering;

public sealed class SegmentStyleTests
{
    private static StyledSegment Seg(TextStyle style) => new("x", style);

    [Test]
    public async Task DefaultIsPhosphorForeground()
    {
        var css = SegmentStyle.ToCss(Seg(TextStyle.Default));

        await Assert.That(css).IsEqualTo("color:#c4d1c8;");
    }

    [Test]
    public async Task IndexedForegroundUsesPalette()
    {
        var css = SegmentStyle.ToCss(Seg(TextStyle.Default with { Foreground = AnsiColor.Indexed(1) }));

        await Assert.That(css).IsEqualTo("color:#e06c75;");
    }

    [Test]
    public async Task IndexedBackgroundAddsPadding()
    {
        var css = SegmentStyle.ToCss(Seg(TextStyle.Default with { Background = AnsiColor.Indexed(4) }));

        await Assert.That(css).IsEqualTo("color:#c4d1c8;background:#61afef;padding:0 2px;");
    }

    [Test]
    public async Task InverseSwapsForegroundAndBackground()
    {
        var css = SegmentStyle.ToCss(Seg(TextStyle.Default with { Foreground = AnsiColor.Indexed(2), Inverse = true }));

        await Assert.That(css).IsEqualTo("color:#090c10;background:#8fc16f;padding:0 2px;");
    }

    [Test]
    public async Task InverseWithDefaultForegroundUsesPhosphorBackground()
    {
        var css = SegmentStyle.ToCss(Seg(TextStyle.Default with { Inverse = true }));

        await Assert.That(css).IsEqualTo("color:#090c10;background:#c4d1c8;padding:0 2px;");
    }

    [Test]
    public async Task BoldAndUnderlineAppend()
    {
        var css = SegmentStyle.ToCss(Seg(TextStyle.Default with { Bold = true, Underline = true }));

        await Assert.That(css).IsEqualTo("color:#c4d1c8;font-weight:700;text-decoration:underline;");
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet run --project tests/SharpClient.Tests/SharpClient.Tests.csproj`
Expected: FAIL — `SegmentStyle` does not exist.

- [ ] **Step 3: Write minimal implementation**

Create `src/SharpClient.Core/Rendering/SegmentStyle.cs`:

```csharp
using System.Text;

namespace SharpClient.Core.Rendering;

public static class SegmentStyle
{
    private const string PhosphorDefault = "#c4d1c8";
    private const string OutputBackground = "#090c10";

    public static string ToCss(StyledSegment segment)
    {
        var style = segment.Style;
        var fg = Resolve(style.Foreground, PhosphorDefault);
        string? bg = style.Background.Kind == AnsiColorKind.Indexed
            ? AnsiPalette.ToHex(style.Background.Index)
            : null;

        if (style.Inverse)
        {
            (fg, bg) = (OutputBackground, fg);
        }

        var sb = new StringBuilder();
        sb.Append("color:").Append(fg).Append(';');
        if (bg is not null)
        {
            sb.Append("background:").Append(bg).Append(';');
            sb.Append("padding:0 2px;");
        }

        if (style.Bold)
        {
            sb.Append("font-weight:700;");
        }

        if (style.Underline)
        {
            sb.Append("text-decoration:underline;");
        }

        return sb.ToString();
    }

    private static string Resolve(AnsiColor color, string fallback) =>
        color.Kind == AnsiColorKind.Indexed ? AnsiPalette.ToHex(color.Index) : fallback;
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet run --project tests/SharpClient.Tests/SharpClient.Tests.csproj`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/SharpClient.Core/Rendering/SegmentStyle.cs tests/SharpClient.Tests/Rendering/SegmentStyleTests.cs
git commit -m "feat(core): add SegmentStyle (StyledSegment to inline CSS render contract)"
```

---

## Task 4: ISessionManager + SessionManager

**Files:**
- Create: `src/SharpClient.Core/Sessions/ISessionManager.cs`
- Create: `src/SharpClient.Core/Sessions/SessionManager.cs`
- Create: `tests/SharpClient.Tests/Sessions/FakeSession.cs`
- Test: `tests/SharpClient.Tests/Sessions/SessionManagerTests.cs`

**Interfaces:**
- Consumes: `ISession` (Task 1).
- Produces:
  - `interface ISessionManager` with `IReadOnlyList<ISession> Sessions { get; } ISession? Active { get; } event Action? Changed; void Add(ISession session); void Activate(ISession session); Task CloseAsync(ISession session);`
  - `sealed class SessionManager : ISessionManager`. `Add` appends and makes the added session active. `Activate` sets `Active` (no-op if not tracked). `CloseAsync` removes the session, disposes it, and if it was active moves `Active` to the first remaining session (or null). `Changed` fires after any mutation.

- [ ] **Step 1: Write the failing test**

Create `tests/SharpClient.Tests/Sessions/FakeSession.cs`:

```csharp
using SharpClient.Core.Connection;
using SharpClient.Core.Sessions;

namespace SharpClient.Tests.Sessions;

public sealed class FakeSession : ISession
{
    public IReadOnlyList<ScrollbackLine> Scrollback { get; } = [];
    public event Action<ScrollbackLine>? LineAppended;
    public event Action<ConnectionState>? StateChanged;
    public ConnectionState State { get; set; } = ConnectionState.Connected;
    public bool Disposed { get; private set; }
    public List<string> Sent { get; } = [];

    public Task ConnectAsync(string host, int port, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task SendAsync(string line)
    {
        Sent.Add(line);
        return Task.CompletedTask;
    }

    public void RaiseState(ConnectionState state)
    {
        State = state;
        StateChanged?.Invoke(state);
    }

    public void Append(ScrollbackLine line) => LineAppended?.Invoke(line);

    public ValueTask DisposeAsync()
    {
        Disposed = true;
        return ValueTask.CompletedTask;
    }
}
```

Create `tests/SharpClient.Tests/Sessions/SessionManagerTests.cs`:

```csharp
using SharpClient.Core.Sessions;

namespace SharpClient.Tests.Sessions;

public sealed class SessionManagerTests
{
    [Test]
    public async Task AddMakesSessionActiveAndFiresChanged()
    {
        var mgr = new SessionManager();
        var changed = 0;
        mgr.Changed += () => changed++;
        var a = new FakeSession();

        mgr.Add(a);

        await Assert.That(mgr.Sessions.Count).IsEqualTo(1);
        await Assert.That(mgr.Active).IsEqualTo(a);
        await Assert.That(changed).IsEqualTo(1);
    }

    [Test]
    public async Task ActivateSwitchesActive()
    {
        var mgr = new SessionManager();
        var a = new FakeSession();
        var b = new FakeSession();
        mgr.Add(a);
        mgr.Add(b);

        mgr.Activate(a);

        await Assert.That(mgr.Active).IsEqualTo(a);
    }

    [Test]
    public async Task CloseActiveDisposesAndMovesActiveToFirstRemaining()
    {
        var mgr = new SessionManager();
        var a = new FakeSession();
        var b = new FakeSession();
        mgr.Add(a);
        mgr.Add(b);
        mgr.Activate(b);

        await mgr.CloseAsync(b);

        await Assert.That(b.Disposed).IsTrue();
        await Assert.That(mgr.Sessions.Count).IsEqualTo(1);
        await Assert.That(mgr.Active).IsEqualTo(a);
    }

    [Test]
    public async Task CloseLastSessionLeavesNoActive()
    {
        var mgr = new SessionManager();
        var a = new FakeSession();
        mgr.Add(a);

        await mgr.CloseAsync(a);

        await Assert.That(mgr.Sessions.Count).IsEqualTo(0);
        await Assert.That(mgr.Active).IsNull();
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet run --project tests/SharpClient.Tests/SharpClient.Tests.csproj`
Expected: FAIL — `SessionManager` / `ISessionManager` do not exist.

- [ ] **Step 3: Write minimal implementation**

Create `src/SharpClient.Core/Sessions/ISessionManager.cs`:

```csharp
namespace SharpClient.Core.Sessions;

public interface ISessionManager
{
    IReadOnlyList<ISession> Sessions { get; }

    ISession? Active { get; }

    event Action? Changed;

    void Add(ISession session);

    void Activate(ISession session);

    Task CloseAsync(ISession session);
}
```

Create `src/SharpClient.Core/Sessions/SessionManager.cs`:

```csharp
namespace SharpClient.Core.Sessions;

public sealed class SessionManager : ISessionManager
{
    private readonly List<ISession> _sessions = [];

    public IReadOnlyList<ISession> Sessions => _sessions;

    public ISession? Active { get; private set; }

    public event Action? Changed;

    public void Add(ISession session)
    {
        _sessions.Add(session);
        Active = session;
        Changed?.Invoke();
    }

    public void Activate(ISession session)
    {
        if (!_sessions.Contains(session))
        {
            return;
        }

        Active = session;
        Changed?.Invoke();
    }

    public async Task CloseAsync(ISession session)
    {
        if (!_sessions.Remove(session))
        {
            return;
        }

        if (Active == session)
        {
            Active = _sessions.Count > 0 ? _sessions[0] : null;
        }

        await session.DisposeAsync();
        Changed?.Invoke();
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet run --project tests/SharpClient.Tests/SharpClient.Tests.csproj`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/SharpClient.Core/Sessions/ISessionManager.cs src/SharpClient.Core/Sessions/SessionManager.cs tests/SharpClient.Tests/Sessions/FakeSession.cs tests/SharpClient.Tests/Sessions/SessionManagerTests.cs
git commit -m "feat(core): add SessionManager (tab lifecycle) behind ISessionManager"
```

---

## Task 5: SessionsViewModel

**Files:**
- Create: `src/SharpClient.Core/Presentation/SessionsViewModel.cs`
- Test: `tests/SharpClient.Tests/Presentation/SessionsViewModelTests.cs`

**Interfaces:**
- Consumes: `ISessionManager`, `ISession` (Tasks 1, 4), `ConnectionState`.
- Produces: `sealed class SessionsViewModel(ISessionManager manager)` with:
  - `IReadOnlyList<ISession> Tabs { get; }` (= manager.Sessions)
  - `ISession? Active { get; }` (= manager.Active)
  - `string Input { get; set; }`
  - `bool CanSend { get; }` — true only when `Active?.State == Connected` and `Input` is non-whitespace.
  - `IReadOnlyList<string> History { get; }` — commands sent on the active session, most-recent first, deduped, capped at 20.
  - `event Action? Changed;` — fires when the manager changes or input/history changes.
  - `Task SendAsync()` — if `CanSend`, send `Input` to `Active`, push to history, clear `Input`, fire `Changed`.
  - `void Select(ISession session)` → `manager.Activate`.

History is tracked per active session id via the session instance reference.

- [ ] **Step 1: Write the failing test**

Create `tests/SharpClient.Tests/Presentation/SessionsViewModelTests.cs`:

```csharp
using SharpClient.Core.Connection;
using SharpClient.Core.Presentation;
using SharpClient.Core.Sessions;
using SharpClient.Tests.Sessions;

namespace SharpClient.Tests.Presentation;

public sealed class SessionsViewModelTests
{
    [Test]
    public async Task CanSendOnlyWhenConnectedAndInputNonEmpty()
    {
        var mgr = new SessionManager();
        var s = new FakeSession { State = ConnectionState.Connected };
        mgr.Add(s);
        var vm = new SessionsViewModel(mgr);

        await Assert.That(vm.CanSend).IsFalse();
        vm.Input = "look";
        await Assert.That(vm.CanSend).IsTrue();

        s.State = ConnectionState.Error;
        await Assert.That(vm.CanSend).IsFalse();
    }

    [Test]
    public async Task SendDeliversToActiveAndRecordsHistory()
    {
        var mgr = new SessionManager();
        var s = new FakeSession { State = ConnectionState.Connected };
        mgr.Add(s);
        var vm = new SessionsViewModel(mgr) { Input = "north" };

        await vm.SendAsync();

        await Assert.That(s.Sent).IsEquivalentTo(new[] { "north" });
        await Assert.That(vm.Input).IsEqualTo(string.Empty);
        await Assert.That(vm.History).IsEquivalentTo(new[] { "north" });
    }

    [Test]
    public async Task HistoryIsMostRecentFirstAndDeduped()
    {
        var mgr = new SessionManager();
        var s = new FakeSession { State = ConnectionState.Connected };
        mgr.Add(s);
        var vm = new SessionsViewModel(mgr);

        vm.Input = "look"; await vm.SendAsync();
        vm.Input = "north"; await vm.SendAsync();
        vm.Input = "look"; await vm.SendAsync();

        await Assert.That(vm.History).IsEquivalentTo(new[] { "look", "north" });
    }

    [Test]
    public async Task SelectActivatesSessionInManager()
    {
        var mgr = new SessionManager();
        var a = new FakeSession();
        var b = new FakeSession();
        mgr.Add(a);
        mgr.Add(b);
        var vm = new SessionsViewModel(mgr);

        vm.Select(a);

        await Assert.That(vm.Active).IsEqualTo(a);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet run --project tests/SharpClient.Tests/SharpClient.Tests.csproj`
Expected: FAIL — `SessionsViewModel` does not exist.

- [ ] **Step 3: Write minimal implementation**

Create `src/SharpClient.Core/Presentation/SessionsViewModel.cs`:

```csharp
using SharpClient.Core.Connection;
using SharpClient.Core.Sessions;

namespace SharpClient.Core.Presentation;

public sealed class SessionsViewModel
{
    private const int HistoryCap = 20;

    private readonly ISessionManager _manager;
    private readonly Dictionary<ISession, List<string>> _histories = [];

    public SessionsViewModel(ISessionManager manager)
    {
        _manager = manager;
        _manager.Changed += () => Changed?.Invoke();
    }

    public IReadOnlyList<ISession> Tabs => _manager.Sessions;

    public ISession? Active => _manager.Active;

    public string Input { get; set; } = string.Empty;

    public bool CanSend => Active?.State == ConnectionState.Connected && !string.IsNullOrWhiteSpace(Input);

    public IReadOnlyList<string> History =>
        Active is not null && _histories.TryGetValue(Active, out var h) ? h : [];

    public event Action? Changed;

    public void Select(ISession session) => _manager.Activate(session);

    public async Task SendAsync()
    {
        if (!CanSend || Active is null)
        {
            return;
        }

        var command = Input.Trim();
        var active = Active;
        await active.SendAsync(command);

        if (!_histories.TryGetValue(active, out var history))
        {
            history = [];
            _histories[active] = history;
        }

        history.RemoveAll(c => c == command);
        history.Insert(0, command);
        if (history.Count > HistoryCap)
        {
            history.RemoveRange(HistoryCap, history.Count - HistoryCap);
        }

        Input = string.Empty;
        Changed?.Invoke();
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet run --project tests/SharpClient.Tests/SharpClient.Tests.csproj`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/SharpClient.Core/Presentation tests/SharpClient.Tests/Presentation
git commit -m "feat(core): add SessionsViewModel (tabs, input, history) over interfaces"
```

---

## Task 6: bUnit test project + OutputView component

**Files:**
- Create: `tests/SharpClient.UI.Tests/SharpClient.UI.Tests.csproj`
- Create: `src/SharpClient.UI/Components/OutputView.razor`
- Create: `tests/SharpClient.UI.Tests/OutputViewTests.cs`
- Modify: `SharpClient.slnx` (add the UI test project)

**Interfaces:**
- Consumes: `ScrollbackLine`, `StyledSegment` (Phase 1 / Task 1), `SegmentStyle.ToCss` (Task 3).
- Produces: `OutputView` Razor component with a `[Parameter] public IReadOnlyList<ScrollbackLine> Lines { get; set; }` that renders each line as a `<div>` containing one `<span style="...">` per segment, the `style` coming from `SegmentStyle.ToCss`. Empty segment text renders a non-breaking space so blank lines keep height.

- [ ] **Step 1: Create the UI test project and wire it up**

Run:
```bash
cd /home/grave/RiderProjects/SharpClient
dotnet new classlib -n SharpClient.UI.Tests -o tests/SharpClient.UI.Tests -f net10.0 --no-restore
rm tests/SharpClient.UI.Tests/Class1.cs
dotnet add tests/SharpClient.UI.Tests/SharpClient.UI.Tests.csproj reference src/SharpClient.UI/SharpClient.UI.csproj src/SharpClient.Core/SharpClient.Core.csproj
dotnet add tests/SharpClient.UI.Tests/SharpClient.UI.Tests.csproj package TUnit
dotnet add tests/SharpClient.UI.Tests/SharpClient.UI.Tests.csproj package bunit --version 2.7.2
dotnet sln SharpClient.slnx add tests/SharpClient.UI.Tests/SharpClient.UI.Tests.csproj
```

Then edit `tests/SharpClient.UI.Tests/SharpClient.UI.Tests.csproj` so its `<PropertyGroup>` contains exactly:
```xml
<TargetFramework>net10.0</TargetFramework>
<OutputType>Exe</OutputType>
<IsPackable>false</IsPackable>
<TestingPlatformDotnetTestSupport>true</TestingPlatformDotnetTestSupport>
```

Expected: restore succeeds.

- [ ] **Step 2: Write the failing test**

Create `tests/SharpClient.UI.Tests/OutputViewTests.cs`:

```csharp
using Bunit;
using SharpClient.Core.Rendering;
using SharpClient.Core.Sessions;
using SharpClient.UI.Components;

namespace SharpClient.UI.Tests;

public sealed class OutputViewTests
{
    private static ScrollbackLine Line(params StyledSegment[] segments) => new(segments);

    [Test]
    public async Task RendersOneSpanPerSegmentWithRenderContractStyle()
    {
        using var ctx = new BunitContext();
        var line = Line(
            new StyledSegment("red", TextStyle.Default with { Foreground = AnsiColor.Indexed(1) }),
            new StyledSegment(" plain", TextStyle.Default));

        var cut = ctx.Render<OutputView>(p => p.Add(c => c.Lines, new[] { line }));

        var spans = cut.FindAll("span");
        await Assert.That(spans.Count).IsEqualTo(2);
        await Assert.That(spans[0].GetAttribute("style")).IsEqualTo("color:#e06c75;");
        await Assert.That(spans[0].TextContent).IsEqualTo("red");
        await Assert.That(spans[1].GetAttribute("style")).IsEqualTo("color:#c4d1c8;");
    }

    [Test]
    public async Task EmptySegmentRendersNonBreakingSpace()
    {
        using var ctx = new BunitContext();
        var line = Line(new StyledSegment(string.Empty, TextStyle.Default));

        var cut = ctx.Render<OutputView>(p => p.Add(c => c.Lines, new[] { line }));

        await Assert.That(cut.Find("span").TextContent).IsEqualTo(" ");
    }
}
```

- [ ] **Step 3: Run test to verify it fails**

Run: `dotnet run --project tests/SharpClient.UI.Tests/SharpClient.UI.Tests.csproj`
Expected: FAIL — `OutputView` does not exist.

- [ ] **Step 4: Write minimal implementation**

Create `src/SharpClient.UI/Components/OutputView.razor`:

```razor
@using SharpClient.Core.Rendering
@using SharpClient.Core.Sessions

<div class="sc-output">
    @foreach (var line in Lines)
    {
        <div class="sc-line">
            @foreach (var segment in line.Segments)
            {
                <span style="@SegmentStyle.ToCss(segment)">@(segment.Text.Length == 0 ? " " : segment.Text)</span>
            }
        </div>
    }
</div>

@code {
    [Parameter]
    public IReadOnlyList<ScrollbackLine> Lines { get; set; } = [];
}
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet run --project tests/SharpClient.UI.Tests/SharpClient.UI.Tests.csproj`
Expected: PASS — 2 component tests green.

- [ ] **Step 6: Commit**

```bash
git add tests/SharpClient.UI.Tests src/SharpClient.UI/Components/OutputView.razor SharpClient.slnx
git commit -m "feat(ui): add bUnit test project + OutputView component (render contract)"
```

---

## Done — UI Foundation deliverable

After Task 6 the interface-based foundation is in place and fully tested without
a device: extensible connection states behind `ITelnetConnection`/`ISession`, the
design's ANSI palette and `StyledSegment`→CSS contract, a tab manager and session
view model over interfaces, and the first bUnit-tested Razor component.

### Subsequent plans (the rest of the spec's phasing)

- **Plan 3 — Session shell components:** `SessionTabs`, `InputBar`, the session
  screen wiring `SessionsViewModel`; CSS tokens (`app.css`) from
  `design-tokens.md`; phone-nav / tablet-rail shell. bUnit + view-model tests.
- **Plan 4 — Worlds & Characters:** `IWorldStore`/`WorldStore`, `ISecretStore`,
  `IAppStorage` platform abstractions + MAUI implementations, `WorldManager` UI,
  Connect flow.
- **Plan 5 — Negotiation + Protocol Panel** and **Plan 6 — Triggers/aliases +
  logging**, per the spec.

Each subsequent plan is interface-based and TDD on the same pattern.
