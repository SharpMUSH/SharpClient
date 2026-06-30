# Phase 2 UI Components Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the SharpClient session-screen UI (Blazor components: SessionTabs, InputBar, SessionScreen), wire a multi-session demo into SharpClient.Web, and extend session identity with CharacterName/WorldName.

**Architecture:** ISession gains two read-only identity properties (CharacterName, WorldName) via default-param ctor extension in Session; SessionsViewModel gains CloseAsync delegation. Three Razor components in SharpClient.UI (SessionTabs, InputBar, SessionScreen) compose the session screen, all accepting a SessionsViewModel parameter. A DemoSession in SharpClient.Web seeds the web host with three state-diverse sessions. bUnit tests in SharpClient.UI.Tests cover the new components via a self-contained UiFakeSession.

**Tech Stack:** .NET 10 (net10.0), TUnit 1.57, bUnit 2.7.2, Blazor Server (SharpClient.Web), Razor Class Library (SharpClient.UI).

## Global Constraints

- Target framework `net10.0` for all non-MAUI projects.
- `TreatWarningsAsErrors=true` solution-wide — every project builds at **0 warnings**.
- `Nullable=enable`, `ImplicitUsings=enable` globally — never redeclare in csproj.
- `EnforceCodeStyleInBuild=true`: file-scoped namespaces required (`csharp_style_namespace_declarations = file_scoped:warning`), usings outside namespace, accessibility modifiers always required (`dotnet_style_require_accessibility_modifiers = always:warning`).
- Test framework is TUnit: `[Test]`, `await Assert.That(actual).IsEqualTo(expected)`.
- bUnit tests are C# files using `Bunit.BunitContext` instantiated per test.
- Run Core tests: `dotnet run --project tests/SharpClient.Tests/SharpClient.Tests.csproj`.
- Run UI tests: `dotnet run --project tests/SharpClient.UI.Tests/SharpClient.UI.Tests.csproj`.
- Build web: `dotnet build src/SharpClient.Web/SharpClient.Web.csproj`.
- All paths relative to `/home/grave/RiderProjects/SharpClient`.
- Connection-state palette (spec verbatim): Connected=#8fc16f, Connecting/Reconnecting=#e5c07b, Disconnected=#5c6672, Error=#e06c75.
- Design tokens panel bg `--panel #11151b`, accent `--acc #9b7ed4`.

---

## File Structure

### Created / Modified
- **Modify:** `src/SharpClient.Core/Sessions/ISession.cs` — add `CharacterName` and `WorldName` string properties.
- **Modify:** `src/SharpClient.Core/Sessions/Session.cs` — extend ctor with optional `characterName=""` and `worldName=""` default params; store and expose.
- **Modify:** `src/SharpClient.Core/Presentation/SessionsViewModel.cs` — add `Task CloseAsync(ISession session)` delegating to `_manager.CloseAsync`.
- **Modify:** `tests/SharpClient.Tests/Sessions/FakeSession.cs` — add settable `CharacterName` and `WorldName` (default `""`).
- **Create:** `src/SharpClient.UI/Components/SessionTabs.razor` — horizontal tab strip driven by `SessionsViewModel`.
- **Create:** `src/SharpClient.UI/Components/InputBar.razor` — text input + Send button driven by `SessionsViewModel`.
- **Create:** `src/SharpClient.UI/Components/SessionScreen.razor` — composes SessionTabs + OutputView + InputBar; IDisposable lifecycle.
- **Create:** `tests/SharpClient.UI.Tests/UiFakeSession.cs` — self-contained ISession test double for UI.Tests project.
- **Create:** `tests/SharpClient.UI.Tests/SessionTabsTests.cs` — bUnit tests for SessionTabs.
- **Create:** `tests/SharpClient.UI.Tests/InputBarTests.cs` — bUnit tests for InputBar.
- **Create:** `src/SharpClient.Web/DemoSession.cs` — ISession implementation seeded with sample ANSI scrollback.
- **Modify:** `src/SharpClient.Web/Program.cs` — register SessionManager + SessionsViewModel, seed 3 demo sessions.
- **Modify:** `src/SharpClient.Web/Components/Pages/Home.razor` — inject `SessionsViewModel`, render `<SessionScreen Vm="@Vm"/>`.
- **Modify:** `src/SharpClient.Web/wwwroot/app.css` — add styles for `.sc-tab`, `.sc-tab-active`, `.sc-state-dot`, `.sc-tab-bar`, `.sc-input-bar`, `.sc-session-screen`, `.sc-empty-state`.

---

## Task 1: Extend Session Identity in Core

**Files:**
- Modify: `src/SharpClient.Core/Sessions/ISession.cs`
- Modify: `src/SharpClient.Core/Sessions/Session.cs`
- Modify: `src/SharpClient.Core/Presentation/SessionsViewModel.cs`
- Modify: `tests/SharpClient.Tests/Sessions/FakeSession.cs`

**Interfaces:**
- Consumes: existing `ISession`, `Session`, `SessionsViewModel`, `FakeSession`.
- Produces:
  - `ISession` gains `string CharacterName { get; }` and `string WorldName { get; }`.
  - `Session(ITelnetConnection connection, string characterName = "", string worldName = "")` — default params keep `new Session(connection)` compiling.
  - `SessionsViewModel.CloseAsync(ISession session)` — delegates to `_manager.CloseAsync(session)`.
  - `FakeSession` gains `public string CharacterName { get; set; } = "";` and `public string WorldName { get; set; } = "";`.

- [ ] **Step 1: Update ISession.cs**

Replace `src/SharpClient.Core/Sessions/ISession.cs` with:

```csharp
using SharpClient.Core.Connection;

namespace SharpClient.Core.Sessions;

public interface ISession : IAsyncDisposable
{
    public IReadOnlyList<ScrollbackLine> Scrollback { get; }

    public event Action<ScrollbackLine>? LineAppended;

    public event Action<ConnectionState>? StateChanged;

    public ConnectionState State { get; }

    public string CharacterName { get; }

    public string WorldName { get; }

    public Task ConnectAsync(string host, int port, CancellationToken cancellationToken = default);

    public Task SendAsync(string line);
}
```

- [ ] **Step 2: Update Session.cs**

Replace `src/SharpClient.Core/Sessions/Session.cs` with:

```csharp
using SharpClient.Core.Connection;
using SharpClient.Core.Rendering;

namespace SharpClient.Core.Sessions;

public sealed record ScrollbackLine(IReadOnlyList<StyledSegment> Segments);

public sealed class Session : ISession
{
    private readonly ITelnetConnection _connection;
    private readonly List<ScrollbackLine> _scrollback = [];

    public Session(ITelnetConnection connection, string characterName = "", string worldName = "")
    {
        _connection = connection;
        CharacterName = characterName;
        WorldName = worldName;
        _connection.LineReceived += OnLineReceived;
        _connection.StateChanged += OnStateChanged;
    }

    public IReadOnlyList<ScrollbackLine> Scrollback => _scrollback;

    public event Action<ScrollbackLine>? LineAppended;

    public event Action<ConnectionState>? StateChanged;

    public ConnectionState State => _connection.State;

    public string CharacterName { get; }

    public string WorldName { get; }

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

- [ ] **Step 3: Update FakeSession.cs (in SharpClient.Tests)**

Replace `tests/SharpClient.Tests/Sessions/FakeSession.cs` with:

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
    public string CharacterName { get; set; } = string.Empty;
    public string WorldName { get; set; } = string.Empty;
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

- [ ] **Step 4: Add CloseAsync to SessionsViewModel.cs**

Add after `Select` method:

```csharp
public Task CloseAsync(ISession session) => _manager.CloseAsync(session);
```

- [ ] **Step 5: Run Core tests to verify 75 still pass**

Run: `dotnet run --project tests/SharpClient.Tests/SharpClient.Tests.csproj`
Expected: All 75 pass, 0 warnings.

- [ ] **Step 6: Commit**

```bash
git add src/SharpClient.Core/Sessions/ISession.cs src/SharpClient.Core/Sessions/Session.cs src/SharpClient.Core/Presentation/SessionsViewModel.cs tests/SharpClient.Tests/Sessions/FakeSession.cs
git commit -m "feat(core): add CharacterName/WorldName to ISession + SessionsViewModel.CloseAsync"
```

---

## Task 2: UI Components (SessionTabs, InputBar, SessionScreen)

**Files:**
- Create: `src/SharpClient.UI/Components/SessionTabs.razor`
- Create: `src/SharpClient.UI/Components/InputBar.razor`
- Create: `src/SharpClient.UI/Components/SessionScreen.razor`

**Interfaces:**
- Consumes: `SessionsViewModel` (Task 1), `ISession`, `ConnectionState`, `OutputView` (existing), `ScrollbackLine`.
- Produces:
  - `SessionTabs`: `[Parameter] SessionsViewModel Vm` — renders Vm.Tabs as tab strip; state dot per ConnectionState; active tab gets `sc-tab-active` class; click → `Vm.Select(session)`; × → `await Vm.CloseAsync(session)`.
  - `InputBar`: `[Parameter] SessionsViewModel Vm` — `@bind` input to `Vm.Input`; Send button disabled when `!Vm.CanSend`; Enter/Send → `await Vm.SendAsync()` then `StateHasChanged()`.
  - `SessionScreen`: `[Parameter] SessionsViewModel Vm`; `@implements IDisposable`; subscribes to `Vm.Changed` → `InvokeAsync(StateHasChanged)`; empty state when `Vm.Active is null`.

- [ ] **Step 1: Create SessionTabs.razor**

Create `src/SharpClient.UI/Components/SessionTabs.razor`:

```razor
@using SharpClient.Core.Connection
@using SharpClient.Core.Presentation
@using SharpClient.Core.Sessions

<div class="sc-tab-bar">
    @foreach (var session in Vm.Tabs)
    {
        var isActive = session == Vm.Active;
        <div class="sc-tab @(isActive ? "sc-tab-active" : string.Empty)" @onclick="() => Vm.Select(session)">
            <span class="sc-state-dot" style="background:@StateDotColor(session.State)"></span>
            <span class="sc-tab-name">@session.CharacterName</span>
            @if (!string.IsNullOrEmpty(session.WorldName))
            {
                <span class="sc-tab-world">@session.WorldName</span>
            }
            <button class="sc-tab-close" @onclick:stopPropagation="true" @onclick="async () => await Vm.CloseAsync(session)">×</button>
        </div>
    }
</div>

@code {
    [Parameter]
    public SessionsViewModel Vm { get; set; } = null!;

    private static string StateDotColor(ConnectionState state) => state switch
    {
        ConnectionState.Connected => "#8fc16f",
        ConnectionState.Connecting => "#e5c07b",
        ConnectionState.Reconnecting => "#e5c07b",
        ConnectionState.Disconnected => "#5c6672",
        ConnectionState.Error => "#e06c75",
        _ => "#5c6672",
    };
}
```

- [ ] **Step 2: Create InputBar.razor**

Create `src/SharpClient.UI/Components/InputBar.razor`:

```razor
@using SharpClient.Core.Presentation

<div class="sc-input-bar">
    <input class="sc-input"
           type="text"
           value="@Vm.Input"
           @oninput="OnInput"
           @onkeydown="OnKeyDown"
           placeholder="Enter command…" />
    <button class="sc-send-btn"
            disabled="@(!Vm.CanSend)"
            @onclick="SendAsync">Send</button>
</div>

@code {
    [Parameter]
    public SessionsViewModel Vm { get; set; } = null!;

    private void OnInput(ChangeEventArgs e) => Vm.Input = e.Value?.ToString() ?? string.Empty;

    private async Task OnKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter")
        {
            await SendAsync();
        }
    }

    private async Task SendAsync()
    {
        await Vm.SendAsync();
        StateHasChanged();
    }
}
```

- [ ] **Step 3: Create SessionScreen.razor**

Create `src/SharpClient.UI/Components/SessionScreen.razor`:

```razor
@using SharpClient.Core.Presentation
@using SharpClient.Core.Sessions

@implements IDisposable

<div class="sc-session-screen">
    <SessionTabs Vm="Vm" />
    <div class="sc-output-area">
        @if (Vm.Active is null)
        {
            <div class="sc-empty-state">
                <p>No active session. Open a session to get started.</p>
            </div>
        }
        else
        {
            <OutputView Lines="Vm.Active.Scrollback" />
        }
    </div>
    <InputBar Vm="Vm" />
</div>

@code {
    [Parameter]
    public SessionsViewModel Vm { get; set; } = null!;

    protected override void OnInitialized()
    {
        Vm.Changed += OnVmChanged;
    }

    private void OnVmChanged() => InvokeAsync(StateHasChanged);

    public void Dispose()
    {
        Vm.Changed -= OnVmChanged;
    }
}
```

- [ ] **Step 4: Build UI project to verify 0 warnings**

Run: `dotnet build src/SharpClient.UI/SharpClient.UI.csproj`
Expected: Build succeeded, 0 warnings.

- [ ] **Step 5: Commit**

```bash
git add src/SharpClient.UI/Components/SessionTabs.razor src/SharpClient.UI/Components/InputBar.razor src/SharpClient.UI/Components/SessionScreen.razor
git commit -m "feat(ui): add SessionTabs, InputBar, SessionScreen Razor components"
```

---

## Task 3: bUnit Tests for New Components

**Files:**
- Create: `tests/SharpClient.UI.Tests/UiFakeSession.cs`
- Create: `tests/SharpClient.UI.Tests/SessionTabsTests.cs`
- Create: `tests/SharpClient.UI.Tests/InputBarTests.cs`

**Interfaces:**
- Consumes: `SessionsViewModel`, `SessionManager` (from Core), `ISession`, `ConnectionState`, `SessionTabs`, `InputBar`.
- Produces:
  - `UiFakeSession : ISession` with settable `State`, `CharacterName`, `WorldName`, `Scrollback`; `Sent` list; no-op events and dispose.
  - `SessionTabsTests`: 2 sessions → 2 tabs with right names; error tab dot is `#e06c75`; active tab has `sc-tab-active`.
  - `InputBarTests`: disabled when empty; enabled after setting Input; click Send → Sent contains "look" and Input cleared.

- [ ] **Step 1: Create UiFakeSession.cs**

Create `tests/SharpClient.UI.Tests/UiFakeSession.cs`:

```csharp
using SharpClient.Core.Connection;
using SharpClient.Core.Sessions;

namespace SharpClient.UI.Tests;

public sealed class UiFakeSession : ISession
{
    public ConnectionState State { get; set; } = ConnectionState.Connected;
    public string CharacterName { get; set; } = string.Empty;
    public string WorldName { get; set; } = string.Empty;
    public IReadOnlyList<ScrollbackLine> Scrollback { get; set; } = [];
    public List<string> Sent { get; } = [];

    public event Action<ScrollbackLine>? LineAppended;
    public event Action<ConnectionState>? StateChanged;

    public Task ConnectAsync(string host, int port, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task SendAsync(string line)
    {
        Sent.Add(line);
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
```

- [ ] **Step 2: Create SessionTabsTests.cs**

Create `tests/SharpClient.UI.Tests/SessionTabsTests.cs`:

```csharp
using Bunit;
using SharpClient.Core.Connection;
using SharpClient.Core.Presentation;
using SharpClient.Core.Sessions;
using SharpClient.UI.Components;

namespace SharpClient.UI.Tests;

public sealed class SessionTabsTests
{
    [Test]
    public async Task RendersOneTabPerSession()
    {
        using var ctx = new BunitContext();
        var mgr = new SessionManager();
        var s1 = new UiFakeSession { CharacterName = "Vesper", WorldName = "Sindome", State = ConnectionState.Connected };
        var s2 = new UiFakeSession { CharacterName = "Thorne", WorldName = "GrapevineMUD", State = ConnectionState.Error };
        mgr.Add(s1);
        mgr.Add(s2);
        var vm = new SessionsViewModel(mgr);

        var cut = ctx.Render<SessionTabs>(p => p.Add(c => c.Vm, vm));

        var tabs = cut.FindAll(".sc-tab");
        await Assert.That(tabs.Count).IsEqualTo(2);
        await Assert.That(tabs[0].TextContent).Contains("Vesper");
        await Assert.That(tabs[1].TextContent).Contains("Thorne");
    }

    [Test]
    public async Task ErrorTabDotUsesRedColor()
    {
        using var ctx = new BunitContext();
        var mgr = new SessionManager();
        var s1 = new UiFakeSession { CharacterName = "Vesper", State = ConnectionState.Connected };
        var s2 = new UiFakeSession { CharacterName = "Thorne", State = ConnectionState.Error };
        mgr.Add(s1);
        mgr.Add(s2);
        var vm = new SessionsViewModel(mgr);

        var cut = ctx.Render<SessionTabs>(p => p.Add(c => c.Vm, vm));

        var dots = cut.FindAll(".sc-state-dot");
        await Assert.That(dots[1].GetAttribute("style")).Contains("#e06c75");
    }

    [Test]
    public async Task ActiveTabHasActiveClass()
    {
        using var ctx = new BunitContext();
        var mgr = new SessionManager();
        var s1 = new UiFakeSession { CharacterName = "Vesper", State = ConnectionState.Connected };
        var s2 = new UiFakeSession { CharacterName = "Thorne", State = ConnectionState.Error };
        mgr.Add(s1);
        mgr.Add(s2);
        var vm = new SessionsViewModel(mgr);
        vm.Select(s1);

        var cut = ctx.Render<SessionTabs>(p => p.Add(c => c.Vm, vm));

        var tabs = cut.FindAll(".sc-tab");
        await Assert.That(tabs[0].ClassList).Contains("sc-tab-active");
        await Assert.That(tabs[1].ClassList).DoesNotContain("sc-tab-active");
    }
}
```

- [ ] **Step 3: Create InputBarTests.cs**

Create `tests/SharpClient.UI.Tests/InputBarTests.cs`:

```csharp
using Bunit;
using SharpClient.Core.Connection;
using SharpClient.Core.Presentation;
using SharpClient.Core.Sessions;
using SharpClient.UI.Components;

namespace SharpClient.UI.Tests;

public sealed class InputBarTests
{
    [Test]
    public async Task SendButtonDisabledWhenInputEmpty()
    {
        using var ctx = new BunitContext();
        var mgr = new SessionManager();
        var s = new UiFakeSession { State = ConnectionState.Connected };
        mgr.Add(s);
        var vm = new SessionsViewModel(mgr);

        var cut = ctx.Render<InputBar>(p => p.Add(c => c.Vm, vm));

        var btn = cut.Find("button");
        await Assert.That(btn.HasAttribute("disabled")).IsTrue();
    }

    [Test]
    public async Task SendButtonEnabledWhenInputNonEmpty()
    {
        using var ctx = new BunitContext();
        var mgr = new SessionManager();
        var s = new UiFakeSession { State = ConnectionState.Connected };
        mgr.Add(s);
        var vm = new SessionsViewModel(mgr) { Input = "look" };

        var cut = ctx.Render<InputBar>(p => p.Add(c => c.Vm, vm));

        var btn = cut.Find("button");
        await Assert.That(btn.HasAttribute("disabled")).IsFalse();
    }

    [Test]
    public async Task ClickSendDeliversCommandAndClearsInput()
    {
        using var ctx = new BunitContext();
        var mgr = new SessionManager();
        var s = new UiFakeSession { State = ConnectionState.Connected };
        mgr.Add(s);
        var vm = new SessionsViewModel(mgr) { Input = "look" };

        var cut = ctx.Render<InputBar>(p => p.Add(c => c.Vm, vm));

        await cut.Find("button").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        await Assert.That(s.Sent).Contains("look");
        await Assert.That(vm.Input).IsEqualTo(string.Empty);
    }
}
```

- [ ] **Step 4: Run UI tests**

Run: `dotnet run --project tests/SharpClient.UI.Tests/SharpClient.UI.Tests.csproj`
Expected: 5 tests pass (2 from OutputViewTests + 3 from SessionTabsTests + 3 from InputBarTests = 8 total… wait, 2 + 3 + 3 = 8). Actually SessionTabsTests has 3 tests and InputBarTests has 3 tests. Total: 2 + 3 + 3 = 8 UI tests pass.

- [ ] **Step 5: Run Core tests to confirm still 75**

Run: `dotnet run --project tests/SharpClient.Tests/SharpClient.Tests.csproj`
Expected: 75 pass, 0 warnings.

- [ ] **Step 6: Commit**

```bash
git add tests/SharpClient.UI.Tests/UiFakeSession.cs tests/SharpClient.UI.Tests/SessionTabsTests.cs tests/SharpClient.UI.Tests/InputBarTests.cs
git commit -m "feat(ui): add bUnit tests for SessionTabs and InputBar with UiFakeSession"
```

---

## Task 4: Web Host Demo (DemoSession + DI + Home page + CSS)

**Files:**
- Create: `src/SharpClient.Web/DemoSession.cs`
- Modify: `src/SharpClient.Web/Program.cs`
- Modify: `src/SharpClient.Web/Components/Pages/Home.razor`
- Modify: `src/SharpClient.Web/wwwroot/app.css`

**Interfaces:**
- Consumes: `ISession`, `ISessionManager`, `SessionManager`, `SessionsViewModel`, `AnsiParser`, `ScrollbackLine`, `ConnectionState`, `SessionScreen`.
- Produces:
  - `DemoSession : ISession` — seeded scrollback via `AnsiParser.Parse`; settable `State`, `CharacterName`, `WorldName`; `SendAsync` echoes the command as a new scrollback line.
  - `Program.cs` — registers `SessionManager` singleton + `SessionsViewModel` singleton; seeds 3 sessions at startup.
  - `Home.razor` — injects `SessionsViewModel`; renders `<SessionScreen Vm="@Vm"/>`.
  - `app.css` — adds styles for session screen layout, tab bar, state dots, input bar.

- [ ] **Step 1: Create DemoSession.cs**

Create `src/SharpClient.Web/DemoSession.cs`:

```csharp
using SharpClient.Core.Connection;
using SharpClient.Core.Rendering;
using SharpClient.Core.Sessions;

namespace SharpClient.Web;

public sealed class DemoSession : ISession
{
    private readonly List<ScrollbackLine> _scrollback = [];

    public IReadOnlyList<ScrollbackLine> Scrollback => _scrollback;

    public event Action<ScrollbackLine>? LineAppended;
    public event Action<ConnectionState>? StateChanged;

    public ConnectionState State { get; set; } = ConnectionState.Connected;
    public string CharacterName { get; set; } = string.Empty;
    public string WorldName { get; set; } = string.Empty;

    public Task ConnectAsync(string host, int port, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task SendAsync(string line)
    {
        var echo = new ScrollbackLine(AnsiParser.Parse($"[90m> {line}[0m"));
        _scrollback.Add(echo);
        LineAppended?.Invoke(echo);
        return Task.CompletedTask;
    }

    public void AppendLine(string ansiLine)
    {
        var scrollbackLine = new ScrollbackLine(AnsiParser.Parse(ansiLine));
        _scrollback.Add(scrollbackLine);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
```

- [ ] **Step 2: Update Program.cs to register DI and seed demos**

Replace `src/SharpClient.Web/Program.cs` with:

```csharp
using SharpClient.Core.Connection;
using SharpClient.Core.Presentation;
using SharpClient.Core.Sessions;
using SharpClient.Web;
using SharpClient.Web.Components;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddSingleton<SessionManager>();
builder.Services.AddSingleton<ISessionManager>(sp => sp.GetRequiredService<SessionManager>());
builder.Services.AddSingleton<SessionsViewModel>(sp =>
    new SessionsViewModel(sp.GetRequiredService<ISessionManager>()));

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Seed demo sessions.
var sessionManager = app.Services.GetRequiredService<SessionManager>();
var vm = app.Services.GetRequiredService<SessionsViewModel>();

var vesper = new DemoSession { CharacterName = "Vesper", WorldName = "Sindome", State = ConnectionState.Connected };
vesper.AppendLine("[1mRED SECTOR — THE DROME[0m");
vesper.AppendLine("A vast underground arena carved from volcanic basalt, lit by chemical torches.");
vesper.AppendLine("");
vesper.AppendLine("[31mThe walls are slick with condensation and old blood.[0m");
vesper.AppendLine("[92mServer:[0m  Sindome v3.4.1  [92m[online][0m");
vesper.AppendLine("[38;5;208mWARNING:[0m  Low-visibility zone — torches flicker at zone boundary.");
vesper.AppendLine("[1m[36mThe Arena Master[0m steps from the shadows and surveys the pit.");
vesper.AppendLine("[33mGold[0m glints against [34mblue[0m steel; the crowd roars.");
vesper.AppendLine("[93m[SYSTEM][0m  Connection established.  Type [4mhelp[0m to begin.");
vesper.AppendLine("[35mA resonant chime echoes through the stone passages.[0m");
vesper.AppendLine("");
vesper.AppendLine("[38;5;118mHP: 320/320  [38;5;208mMP: 140/200  [38;5;87mSP:  75/100[0m");
vesper.AppendLine("You are in the Gate Tunnel.  Exits: [north] [west]");
vesper.AppendLine("");
vesper.AppendLine("[1m[97m[ Combat Log ][0m");
vesper.AppendLine("[91mDrakar strikes you for 42 points of damage![0m");
vesper.AppendLine("[92mYou strike Drakar for 31 points of damage.[0m");
vesper.AppendLine("[7m ROUNDTIME: 3s [0m");

var thorne = new DemoSession { CharacterName = "Thorne", WorldName = "GrapevineMUD", State = ConnectionState.Connecting };
thorne.AppendLine("[93m[SYSTEM][0m  Connecting to GrapevineMUD…");
thorne.AppendLine("[90mEstablishing secure channel…[0m");

var doran = new DemoSession { CharacterName = "Doran", WorldName = "BatMUD", State = ConnectionState.Error };
doran.AppendLine("[91m[ERROR][0m  Connection lost: remote host closed the connection.");
doran.AppendLine("[90mLast seen: BatMUD Gate, south of Market Square.[0m");

sessionManager.Add(vesper);
sessionManager.Add(thorne);
sessionManager.Add(doran);
vm.Select(vesper);

app.Run();
```

- [ ] **Step 3: Update Home.razor**

Replace `src/SharpClient.Web/Components/Pages/Home.razor` with:

```razor
@page "/"
@rendermode InteractiveServer

@using SharpClient.Core.Presentation
@using SharpClient.UI.Components

@inject SessionsViewModel Vm

<PageTitle>SharpClient</PageTitle>

<SessionScreen Vm="@Vm" />
```

- [ ] **Step 4: Extend app.css with component styles**

Append to `src/SharpClient.Web/wwwroot/app.css`:

```css
/* ── Session Screen Layout ─────────────────────────────────────── */
.sc-session-screen {
    display: flex;
    flex-direction: column;
    height: 100vh;
    background: var(--bg);
}

/* ── Tab Bar ───────────────────────────────────────────────────── */
.sc-tab-bar {
    display: flex;
    flex-direction: row;
    align-items: stretch;
    background: var(--panel);
    border-bottom: 1px solid var(--bd2);
    overflow-x: auto;
    flex-shrink: 0;
    gap: 2px;
    padding: 0 4px;
}

.sc-tab {
    display: flex;
    align-items: center;
    gap: 6px;
    padding: 6px 10px;
    cursor: pointer;
    border-bottom: 2px solid transparent;
    color: var(--dim);
    font-family: var(--ui);
    font-size: 0.8rem;
    white-space: nowrap;
    transition: color 0.1s, border-color 0.1s;
}

.sc-tab:hover {
    color: var(--tx);
    background: rgba(255,255,255,.04);
}

.sc-tab-active {
    color: var(--tx);
    border-bottom-color: var(--acc);
}

.sc-state-dot {
    display: inline-block;
    width: 7px;
    height: 7px;
    border-radius: 50%;
    flex-shrink: 0;
}

.sc-tab-name {
    font-weight: 500;
}

.sc-tab-world {
    color: var(--faint);
    font-size: 0.72rem;
}

.sc-tab-close {
    background: none;
    border: none;
    color: var(--faint);
    cursor: pointer;
    font-size: 0.9rem;
    padding: 0 2px;
    line-height: 1;
    margin-left: 2px;
}

.sc-tab-close:hover {
    color: var(--tx);
}

/* ── Output Area ───────────────────────────────────────────────── */
.sc-output-area {
    flex: 1;
    overflow-y: auto;
    background: var(--outbg);
    padding: 0.75rem 1rem;
}

/* ── Empty State ───────────────────────────────────────────────── */
.sc-empty-state {
    display: flex;
    align-items: center;
    justify-content: center;
    height: 100%;
    color: var(--faint);
    font-family: var(--ui);
    font-size: 0.9rem;
}

/* ── Input Bar ─────────────────────────────────────────────────── */
.sc-input-bar {
    display: flex;
    flex-direction: row;
    align-items: center;
    gap: 8px;
    background: var(--panel);
    border-top: 1px solid var(--bd2);
    padding: 8px 12px;
    flex-shrink: 0;
}

.sc-input {
    flex: 1;
    background: var(--elev);
    border: 1px solid var(--bd2);
    border-radius: 4px;
    color: var(--tx);
    font-family: var(--mono);
    font-size: 13px;
    padding: 6px 10px;
    outline: none;
}

.sc-input:focus {
    border-color: var(--acc-line);
}

.sc-input::placeholder {
    color: var(--faint);
}

.sc-send-btn {
    background: var(--acc);
    border: none;
    border-radius: 4px;
    color: #fff;
    cursor: pointer;
    font-family: var(--ui);
    font-size: 0.8rem;
    font-weight: 600;
    letter-spacing: .04em;
    padding: 6px 16px;
    transition: background 0.1s;
}

.sc-send-btn:hover:not(:disabled) {
    background: var(--acc2);
}

.sc-send-btn:disabled {
    background: var(--elev);
    color: var(--faint);
    cursor: not-allowed;
}
```

- [ ] **Step 5: Build Web project with 0 warnings**

Run: `dotnet build src/SharpClient.Web/SharpClient.Web.csproj`
Expected: Build succeeded, 0 warnings.

- [ ] **Step 6: Run all tests to confirm clean pass**

Run: `dotnet run --project tests/SharpClient.Tests/SharpClient.Tests.csproj`
Expected: 75 pass.

Run: `dotnet run --project tests/SharpClient.UI.Tests/SharpClient.UI.Tests.csproj`
Expected: 8 pass (2 OutputView + 3 SessionTabs + 3 InputBar).

- [ ] **Step 7: Commit**

```bash
git add src/SharpClient.Web/DemoSession.cs src/SharpClient.Web/Program.cs src/SharpClient.Web/Components/Pages/Home.razor src/SharpClient.Web/wwwroot/app.css
git commit -m "feat(web): add DemoSession, 3-session DI seed, SessionScreen on Home page"
```

---

## Task 5: Write Report

**Files:**
- Create: `.superpowers/sdd/lineA-shell-report.md`

- [ ] **Step 1: Write the report**

Write `.superpowers/sdd/lineA-shell-report.md` documenting:
- Components built (SessionTabs, InputBar, SessionScreen)
- ISession change (CharacterName, WorldName; Session default params)
- Test results for both suites
- Web demo description (3 sessions, DemoSession, SessionScreen on Home)
- Anything deferred
