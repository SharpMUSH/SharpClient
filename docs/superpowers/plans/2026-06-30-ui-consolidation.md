# UI Consolidation — Shared RCL Pages, Layout, CSS, and Real Launcher Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Eliminate per-host UI duplication by moving routable pages, layout, and CSS into `SharpClient.UI` (the shared RCL), moving the real `TelnetSessionLauncher` to `SharpClient.Core`, and wiring live session-output re-renders so both the Blazor Server web host and the MAUI Blazor Hybrid Android host render identical UI.

**Architecture:** `SharpClient.UI` is a Razor Class Library referenced by both host projects. All routable pages live in `SharpClient.UI/Pages/`, the layout in `SharpClient.UI/Layout/`, and `app.css` in `SharpClient.UI/wwwroot/app.css` (served as `_content/SharpClient.UI/app.css`). Both hosts wire `AdditionalAssemblies` on the Blazor Router so the RCL pages are discovered; only the Web host applies `@rendermode InteractiveServer` (on `<Routes />`), since MAUI Hybrid does not support render mode directives.

**Tech Stack:** .NET 10, C# 14, Blazor Server (Web), MAUI Blazor Hybrid (Android), Razor Class Library, TUnit, bUnit, TelnetNegotiationCore 2.5.0, Entity Framework Core / SQLite.

## Global Constraints

- Target framework: `net10.0` everywhere; App also targets `net10.0-android`.
- `TreatWarningsAsErrors=true` — zero warnings in all projects including Android.
- Nullable reference types enabled; implicit usings enabled — do not redeclare them.
- File-scoped namespaces in all `.cs` files.
- No raw ESC bytes — use ``.
- No `@rendermode` directives in shared RCL pages (MAUI Hybrid fails to compile with them).
- 171 total tests across three suites must stay green after every commit.
- Keep existing bUnit tests in `tests/SharpClient.UI.Tests/` passing — they test RCL components directly.
- Existing Web-only infra pages (`Error.razor`, `NotFound.razor`, `ReconnectModal.razor`) stay in their host projects.

---

## File Structure

### Files to CREATE

| Path | Responsibility |
|---|---|
| `src/SharpClient.Core/Sessions/TelnetSessionLauncher.cs` | Real `ISessionLauncher` shared by both hosts |
| `src/SharpClient.UI/wwwroot/app.css` | Single shared stylesheet (canonical = Web's version) |
| `src/SharpClient.UI/Layout/MainLayout.razor` | Shared layout; nav order: Worlds `/`, Session `/session`, Settings `/settings` |
| `src/SharpClient.UI/Layout/MainLayout.razor.css` | Stub (all styles in app.css) |
| `src/SharpClient.UI/Pages/SessionPage.razor` | `@page "/session"` — session screen |
| `src/SharpClient.UI/Pages/WorldsPage.razor` | `@page "/"` + `@page "/worlds"` — landing page |
| `src/SharpClient.UI/Pages/SettingsPage.razor` | `@page "/settings"` |
| `src/SharpClient.UI/Pages/RulesPage.razor` | `@page "/worlds/{WorldId:guid}/rules"` |

### Files to DELETE

| Path | Reason |
|---|---|
| `src/SharpClient.Web/wwwroot/app.css` | Moved to RCL |
| `src/SharpClient.App/wwwroot/app.css` | Moved to RCL |
| `src/SharpClient.Web/Components/Layout/MainLayout.razor` | Moved to RCL |
| `src/SharpClient.Web/Components/Layout/MainLayout.razor.css` | Moved to RCL |
| `src/SharpClient.App/Components/Layout/MainLayout.razor` | Moved to RCL |
| `src/SharpClient.App/Components/Layout/MainLayout.razor.css` | Moved to RCL |
| `src/SharpClient.Web/Components/Pages/Home.razor` | Replaced by RCL's `SessionPage.razor` |
| `src/SharpClient.Web/Components/Pages/Worlds.razor` | Replaced by RCL |
| `src/SharpClient.Web/Components/Pages/Settings.razor` | Replaced by RCL |
| `src/SharpClient.Web/Components/Pages/Rules.razor` | Replaced by RCL |
| `src/SharpClient.App/Components/Pages/Home.razor` | Replaced by RCL |
| `src/SharpClient.App/Components/Pages/Worlds.razor` | Replaced by RCL |
| `src/SharpClient.App/Components/Pages/Settings.razor` | Replaced by RCL |
| `src/SharpClient.Web/TelnetSessionLauncher.cs` | Replaced by Core's |
| `src/SharpClient.App/Services/TelnetSessionLauncher.cs` | Replaced by Core's |
| `src/SharpClient.Web/DemoSessionLauncher.cs` | No longer registered |
| `src/SharpClient.Web/DemoSession.cs` | No longer used |

### Files to MODIFY

| Path | Change |
|---|---|
| `src/SharpClient.Core/Presentation/SessionsViewModel.cs` | Add active-session `LineAppended`/`StateChanged` live subscriptions |
| `src/SharpClient.UI/_Imports.razor` | Add usings needed by pages and layout |
| `src/SharpClient.Web/Components/App.razor` | CSS link → `_content/SharpClient.UI/app.css`; `<Routes @rendermode="InteractiveServer" />` |
| `src/SharpClient.Web/Components/Routes.razor` | `AdditionalAssemblies`; `DefaultLayout` → `typeof(MainLayout)` |
| `src/SharpClient.Web/Components/_Imports.razor` | Change layout using to `SharpClient.UI.Layout` |
| `src/SharpClient.Web/Program.cs` | Remove demo seeding/launcher; add `AddTelnetClient`, `ITelnetConnectionFactory`, Core `TelnetSessionLauncher` |
| `src/SharpClient.Web/SharpClient.Web.csproj` | Add `TelnetNegotiationCore` package reference |
| `src/SharpClient.App/wwwroot/index.html` | CSS link → `_content/SharpClient.UI/app.css` |
| `src/SharpClient.App/Components/Routes.razor` | `AdditionalAssemblies`; `DefaultLayout` → `typeof(MainLayout)` |
| `src/SharpClient.App/Components/_Imports.razor` | Change layout using to `SharpClient.UI.Layout` |
| `src/SharpClient.App/MauiProgram.cs` | Use Core `TelnetSessionLauncher`; remove `using SharpClient.App.Services` |
| `tests/SharpClient.Tests/Presentation/SessionsViewModelTests.cs` | Add `LineAppendedOnActiveSessionRaisesVmChanged` test |

---

## Task 1: Core TelnetSessionLauncher + Web DI Cleanup

**Files:**
- Create: `src/SharpClient.Core/Sessions/TelnetSessionLauncher.cs`
- Modify: `src/SharpClient.Web/Program.cs`
- Modify: `src/SharpClient.Web/SharpClient.Web.csproj`
- Modify: `src/SharpClient.App/MauiProgram.cs`
- Delete: `src/SharpClient.Web/TelnetSessionLauncher.cs`
- Delete: `src/SharpClient.App/Services/TelnetSessionLauncher.cs`
- Delete: `src/SharpClient.Web/DemoSessionLauncher.cs`
- Delete: `src/SharpClient.Web/DemoSession.cs`

**Interfaces:**
- Produces: `SharpClient.Core.Sessions.TelnetSessionLauncher` implementing `ISessionLauncher` — both hosts register this as their `ISessionLauncher`.

- [ ] **Step 1: Create `src/SharpClient.Core/Sessions/TelnetSessionLauncher.cs`**

```csharp
using SharpClient.Core.Connection;
using SharpClient.Core.Domain;
using SharpClient.Core.Persistence;

namespace SharpClient.Core.Sessions;

/// <summary>
/// Real <see cref="ISessionLauncher"/> shared by both the Blazor Server web host
/// and the MAUI Blazor Hybrid Android host. Opens a TCP telnet connection via
/// <see cref="ITelnetConnectionFactory"/> (which wraps TelnetNegotiationCore),
/// wraps it in a <see cref="Session"/>, and auto-sends the character's connect
/// string (resolved from <see cref="ISecretStore"/>) immediately after connecting.
/// Disposes the session if the secret-send fails.
/// </summary>
public sealed class TelnetSessionLauncher : ISessionLauncher
{
    private readonly ITelnetConnectionFactory _connFactory;
    private readonly ISecretStore _secrets;

    public TelnetSessionLauncher(ITelnetConnectionFactory connFactory, ISecretStore secrets)
    {
        _connFactory = connFactory;
        _secrets = secrets;
    }

    public async Task<ISession> LaunchAsync(
        World world,
        Character character,
        CancellationToken cancellationToken = default)
    {
        var connection = _connFactory.CreateConnection();
        var session = new Session(connection, character.Name, world.Name);

        await session.ConnectAsync(world.Host, world.Port, cancellationToken);

        try
        {
            if (character.ConnectSecretKey is { } key)
            {
                var secret = await _secrets.GetAsync(key);
                if (!string.IsNullOrWhiteSpace(secret))
                    await session.SendAsync(secret);
            }
        }
        catch
        {
            await session.DisposeAsync();
            throw;
        }

        return session;
    }
}
```

- [ ] **Step 2: Add `TelnetNegotiationCore` to `src/SharpClient.Web/SharpClient.Web.csproj`**

Open `src/SharpClient.Web/SharpClient.Web.csproj`. Inside the existing `<ItemGroup>` that has `<NuGetAuditSuppress .../>`, add a new `<ItemGroup>`:

```xml
  <ItemGroup>
    <PackageReference Include="TelnetNegotiationCore" Version="2.5.0" />
  </ItemGroup>
```

(The TNC package is needed so `AddTelnetClient()` resolves in `Program.cs`.)

- [ ] **Step 3: Rewrite `src/SharpClient.Web/Program.cs`**

Replace the entire file:

```csharp
using SharpClient.Core.Connection;
using SharpClient.Core.Platform;
using SharpClient.Core.Presentation;
using SharpClient.Core.Sessions;
using SharpClient.Web;
using SharpClient.Web.Components;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// ── TNC telnet runtime ─────────────────────────────────────────────────────
builder.Services.AddTelnetClient();
builder.Services.AddSingleton<ITelnetConnectionFactory, TelnetConnectionFactory>();

// ── Platform services ──────────────────────────────────────────────────────
builder.Services.AddSingleton<IPreferences, WebPreferences>();
builder.Services.AddSingleton<SettingsViewModel>(sp =>
    new SettingsViewModel(sp.GetRequiredService<IPreferences>()));

builder.Services.AddSingleton<IAppStorage, WebAppStorage>();
builder.Services.AddSingleton<SharpClient.Core.Persistence.ISecretStore, WebSecretStore>();

// ── Session management ─────────────────────────────────────────────────────
builder.Services.AddSingleton<SessionManager>();
builder.Services.AddSingleton<ISessionManager>(sp => sp.GetRequiredService<SessionManager>());
builder.Services.AddSingleton<SessionsViewModel>(sp =>
    new SessionsViewModel(sp.GetRequiredService<ISessionManager>()));
builder.Services.AddSingleton<ProtocolPanelViewModel>(sp =>
    new ProtocolPanelViewModel(sp.GetRequiredService<ISessionManager>()));

// ── Data / persistence ─────────────────────────────────────────────────────
builder.Services.AddScoped<SharpClient.Data.AppDbContext>();
builder.Services.AddScoped<SharpClient.Core.Persistence.IWorldStore, SharpClient.Data.WorldStore>();

// ── Session launcher (real telnet) ─────────────────────────────────────────
builder.Services.AddScoped<ISessionLauncher, TelnetSessionLauncher>();

// ── View models ────────────────────────────────────────────────────────────
builder.Services.AddScoped<WorldManagerViewModel>(sp => new WorldManagerViewModel(
    sp.GetRequiredService<SharpClient.Core.Persistence.IWorldStore>(),
    sp.GetRequiredService<SharpClient.Core.Persistence.ISecretStore>(),
    sp.GetRequiredService<ISessionManager>(),
    sp.GetRequiredService<ISessionLauncher>()));
builder.Services.AddScoped<TriggerAliasEditorViewModel>(sp =>
    new TriggerAliasEditorViewModel(
        sp.GetRequiredService<SharpClient.Core.Persistence.IWorldStore>()));

var app = builder.Build();

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error", createScopeForErrors: true);

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
```

- [ ] **Step 4: Update `src/SharpClient.App/MauiProgram.cs` — use Core launcher**

Change the `using` at the top and the registration line. In the file's usings block, remove `using SharpClient.App.Services;`. Change the registration from:

```csharp
        builder.Services.AddTransient<ISessionLauncher, TelnetSessionLauncher>();
```

to:

```csharp
        builder.Services.AddTransient<ISessionLauncher, SharpClient.Core.Sessions.TelnetSessionLauncher>();
```

(The fully-qualified name avoids any ambiguity and does not require adding a `using`.)

- [ ] **Step 5: Delete the now-unused files**

```bash
rm src/SharpClient.Web/TelnetSessionLauncher.cs
rm src/SharpClient.App/Services/TelnetSessionLauncher.cs
rm src/SharpClient.Web/DemoSessionLauncher.cs
rm src/SharpClient.Web/DemoSession.cs
```

- [ ] **Step 6: Build both projects to verify zero errors**

```bash
dotnet build src/SharpClient.Web/SharpClient.Web.csproj
dotnet build src/SharpClient.App/SharpClient.App.csproj -f net10.0-android
```

Expected: both build with 0 errors, 0 warnings.

- [ ] **Step 7: Run tests**

```bash
dotnet run --project tests/SharpClient.Tests/SharpClient.Tests.csproj
dotnet run --project tests/SharpClient.UI.Tests/SharpClient.UI.Tests.csproj
dotnet run --project tests/SharpClient.Data.Tests/SharpClient.Data.Tests.csproj
```

Expected: all suites green (122 / 33 / 16 = 171 total).

- [ ] **Step 8: Commit**

```bash
git add src/SharpClient.Core/Sessions/TelnetSessionLauncher.cs \
        src/SharpClient.Web/SharpClient.Web.csproj \
        src/SharpClient.Web/Program.cs \
        src/SharpClient.App/MauiProgram.cs
git rm src/SharpClient.Web/TelnetSessionLauncher.cs \
       src/SharpClient.App/Services/TelnetSessionLauncher.cs \
       src/SharpClient.Web/DemoSessionLauncher.cs \
       src/SharpClient.Web/DemoSession.cs
git commit -m "$(cat <<'EOF'
refactor: move TelnetSessionLauncher to Core; remove demo sessions from Web

- Add SharpClient.Core.Sessions.TelnetSessionLauncher (shared real launcher)
- Wire Web Program.cs with AddTelnetClient + real launcher; strip all demo seeding
- Wire App MauiProgram.cs to Core launcher; delete SharpClient.App.Services.TelnetSessionLauncher
- Delete Web DemoSession, DemoSessionLauncher
- Add TelnetNegotiationCore package reference to Web.csproj so AddTelnetClient() resolves

Co-Authored-By: Claude Sonnet 4.6 <noreply@anthropic.com>
EOF
)"
```

---

## Task 2: SessionsViewModel Live Updates + TUnit Test

**Files:**
- Modify: `src/SharpClient.Core/Presentation/SessionsViewModel.cs`
- Modify: `tests/SharpClient.Tests/Presentation/SessionsViewModelTests.cs`

**Interfaces:**
- Consumes: `ISession.LineAppended` (`event Action<ScrollbackLine>?`), `ISession.StateChanged` (`event Action<ConnectionState>?`), `ISessionManager.Active` (`ISession?`), `ISessionManager.Changed` (`event Action?`)
- Produces: `SessionsViewModel.Changed` now fires whenever the active session appends a line or changes state (in addition to when the manager's session list changes).

- [ ] **Step 1: Write the failing test first**

Add this test to `tests/SharpClient.Tests/Presentation/SessionsViewModelTests.cs` (append inside the class body before the closing `}`):

```csharp
    [Test]
    public async Task LineAppendedOnActiveSessionRaisesVmChanged()
    {
        var mgr = new SessionManager();
        var session = new FakeSession { State = ConnectionState.Connected };
        mgr.Add(session); // makes session active; fires manager.Changed
        var vm = new SessionsViewModel(mgr); // constructor subscribes to LineAppended

        var fired = false;
        vm.Changed += () => fired = true;

        // Simulate server line arriving on the active session.
        session.Append(new ScrollbackLine([]));

        await Assert.That(fired).IsTrue();
    }
```

- [ ] **Step 2: Run the test to confirm it fails**

```bash
dotnet run --project tests/SharpClient.Tests/SharpClient.Tests.csproj -- --filter "LineAppendedOnActiveSessionRaisesVmChanged"
```

Expected: FAIL (the test calls `session.Append(...)` but `SessionsViewModel` never subscribed to `LineAppended`, so `fired` stays `false`).

- [ ] **Step 3: Rewrite `src/SharpClient.Core/Presentation/SessionsViewModel.cs`**

Replace the entire file:

```csharp
using SharpClient.Core.Connection;
using SharpClient.Core.Sessions;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("SharpClient.Tests")]

namespace SharpClient.Core.Presentation;

public sealed class SessionsViewModel
{
    private const int HistoryCap = 20;

    private readonly ISessionManager _manager;
    private readonly Dictionary<ISession, List<string>> _histories = [];
    private ISession? _activeSession;

    public SessionsViewModel(ISessionManager manager)
    {
        _manager = manager;
        _manager.Changed += OnManagerChanged;
        // Wire the initial active session if one is already present.
        TrackActiveSession(_manager.Active);
    }

    internal int TrackedHistoryCount => _histories.Count;

    public IReadOnlyList<ISession> Tabs => _manager.Sessions;

    public ISession? Active => _manager.Active;

    public string Input { get; set; } = string.Empty;

    public bool CanSend => Active?.State == ConnectionState.Connected && !string.IsNullOrWhiteSpace(Input);

    public IReadOnlyList<string> History =>
        Active is not null && _histories.TryGetValue(Active, out var h) ? h : [];

    public event Action? Changed;

    public void Select(ISession session) => _manager.Activate(session);

    public Task CloseAsync(ISession session) => _manager.CloseAsync(session);

    public async Task SendAsync()
    {
        if (!CanSend || Active is null)
            return;

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
            history.RemoveRange(HistoryCap, history.Count - HistoryCap);

        Input = string.Empty;
        Changed?.Invoke();
    }

    private void OnManagerChanged()
    {
        var sessions = _manager.Sessions;
        foreach (var key in _histories.Keys.Where(k => !sessions.Contains(k)).ToList())
            _histories.Remove(key);

        TrackActiveSession(_manager.Active);
        Changed?.Invoke();
    }

    private void TrackActiveSession(ISession? newActive)
    {
        if (ReferenceEquals(newActive, _activeSession))
            return;

        if (_activeSession is not null)
        {
            _activeSession.LineAppended -= OnActiveLineAppended;
            _activeSession.StateChanged -= OnActiveStateChanged;
        }

        _activeSession = newActive;

        if (_activeSession is not null)
        {
            _activeSession.LineAppended += OnActiveLineAppended;
            _activeSession.StateChanged += OnActiveStateChanged;
        }
    }

    private void OnActiveLineAppended(ScrollbackLine _) => Changed?.Invoke();

    private void OnActiveStateChanged(ConnectionState _) => Changed?.Invoke();
}
```

- [ ] **Step 4: Run tests to verify new test passes and no regressions**

```bash
dotnet run --project tests/SharpClient.Tests/SharpClient.Tests.csproj
```

Expected: all 122 tests pass including `LineAppendedOnActiveSessionRaisesVmChanged`.

- [ ] **Step 5: Commit**

```bash
git add src/SharpClient.Core/Presentation/SessionsViewModel.cs \
        tests/SharpClient.Tests/Presentation/SessionsViewModelTests.cs
git commit -m "$(cat <<'EOF'
feat: subscribe SessionsViewModel to active session live events

Track the active ISession; on LineAppended/StateChanged raise Changed so
Blazor components re-render when server lines arrive or connection state
changes. Add TUnit test confirming the wire-up fires after session.Append().

Co-Authored-By: Claude Sonnet 4.6 <noreply@anthropic.com>
EOF
)"
```

---

## Task 3: Move app.css to SharpClient.UI wwwroot

The Web's `app.css` is canonical (it has `--out-fs: 14px` and the trigger/alias editor CSS that the App's copy lacks). Copy it to the RCL; update both hosts to reference `_content/SharpClient.UI/app.css`.

**Files:**
- Create: `src/SharpClient.UI/wwwroot/app.css`
- Modify: `src/SharpClient.Web/Components/App.razor`
- Modify: `src/SharpClient.App/wwwroot/index.html`
- Delete: `src/SharpClient.Web/wwwroot/app.css`
- Delete: `src/SharpClient.App/wwwroot/app.css`

**Interfaces:**
- Produces: static web asset served at `_content/SharpClient.UI/app.css` in both hosts.

- [ ] **Step 1: Create `src/SharpClient.UI/wwwroot/app.css`**

Create directory and file:

```bash
mkdir -p src/SharpClient.UI/wwwroot
```

Write the file as an exact copy of `src/SharpClient.Web/wwwroot/app.css` (the canonical version with all CSS including `--out-fs: 14px` and the trigger/alias CSS that was absent from the App version). The file is already fully defined — copy it byte-for-byte from `src/SharpClient.Web/wwwroot/app.css`.

- [ ] **Step 2: Update `src/SharpClient.Web/Components/App.razor` — CSS link and render mode**

Change the line:

```html
    <link rel="stylesheet" href="@Assets["app.css"]" />
```

to:

```html
    <link rel="stylesheet" href="_content/SharpClient.UI/app.css" />
```

Also, in the `<body>` of `App.razor`, change:

```html
    <Routes />
```

to:

```html
    <Routes @rendermode="InteractiveServer" />
```

After this change, `App.razor` should look like:

```html
<!DOCTYPE html>
<html lang="en">

<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <base href="/" />
    <ResourcePreloader />
    <link rel="preconnect" href="https://fonts.googleapis.com" />
    <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin />
    <link href="https://fonts.googleapis.com/css2?family=JetBrains+Mono:wght@400;700&family=Space+Grotesk:wght@400;500;600&display=swap" rel="stylesheet" />
    <link rel="stylesheet" href="_content/SharpClient.UI/app.css" />
    <link rel="stylesheet" href="@Assets["SharpClient.Web.styles.css"]" />
    <ImportMap />
    <link rel="icon" type="image/png" href="favicon.png" />
    <HeadOutlet />
</head>

<body>
    <Routes @rendermode="InteractiveServer" />
    <ReconnectModal />
    <script src="@Assets["_framework/blazor.web.js"]"></script>
</body>

</html>
```

- [ ] **Step 3: Update `src/SharpClient.App/wwwroot/index.html` — CSS link**

Change the line:

```html
    <link rel="stylesheet" href="app.css" />
```

to:

```html
    <link rel="stylesheet" href="_content/SharpClient.UI/app.css" />
```

- [ ] **Step 4: Delete per-host app.css copies**

```bash
rm src/SharpClient.Web/wwwroot/app.css
rm src/SharpClient.App/wwwroot/app.css
```

- [ ] **Step 5: Build to verify**

```bash
dotnet build src/SharpClient.Web/SharpClient.Web.csproj
dotnet build src/SharpClient.App/SharpClient.App.csproj -f net10.0-android
```

Expected: 0 errors, 0 warnings.

- [ ] **Step 6: Commit**

```bash
git add src/SharpClient.UI/wwwroot/app.css \
        src/SharpClient.Web/Components/App.razor \
        src/SharpClient.App/wwwroot/index.html
git rm src/SharpClient.Web/wwwroot/app.css \
       src/SharpClient.App/wwwroot/app.css
git commit -m "$(cat <<'EOF'
refactor: consolidate app.css into SharpClient.UI wwwroot

Both hosts now reference _content/SharpClient.UI/app.css. Removed per-host
copies. Also apply @rendermode="InteractiveServer" on <Routes /> in Web's
App.razor so the render mode moves from individual pages to the host boundary.

Co-Authored-By: Claude Sonnet 4.6 <noreply@anthropic.com>
EOF
)"
```

---

## Task 4: Move MainLayout to SharpClient.UI

**Files:**
- Create: `src/SharpClient.UI/Layout/MainLayout.razor`
- Create: `src/SharpClient.UI/Layout/MainLayout.razor.css`
- Modify: `src/SharpClient.UI/_Imports.razor`
- Modify: `src/SharpClient.Web/Components/_Imports.razor`
- Modify: `src/SharpClient.App/Components/_Imports.razor`
- Delete: `src/SharpClient.Web/Components/Layout/MainLayout.razor`
- Delete: `src/SharpClient.Web/Components/Layout/MainLayout.razor.css`
- Delete: `src/SharpClient.App/Components/Layout/MainLayout.razor`
- Delete: `src/SharpClient.App/Components/Layout/MainLayout.razor.css`

**Interfaces:**
- Produces: `SharpClient.UI.Layout.MainLayout` — a `LayoutComponentBase` that both hosts reference via `typeof(MainLayout)` (after updating `_Imports.razor` to include `@using SharpClient.UI.Layout`).

- [ ] **Step 1: Create `src/SharpClient.UI/Layout/MainLayout.razor`**

The nav order changes: Worlds (`/`) first, then Session (`/session`), then Settings (`/settings`).

```razor
@inherits LayoutComponentBase
@using SharpClient.Core.Presentation
@inject SettingsViewModel SettingsVm
@implements IDisposable

<div class="sc-shell @(SettingsVm.Glow ? "sc-glow" : "") @(SettingsVm.Scanlines ? "sc-scanlines" : "")"
     style="@SettingsVm.RootStyleVariables">

    <nav class="sc-nav">
        <a href="/" class="sc-nav-link">Worlds</a>
        <a href="/session" class="sc-nav-link">Session</a>
        <a href="/settings" class="sc-nav-link">Settings</a>
    </nav>

    @Body

    <div id="blazor-error-ui" data-nosnippet>
        An unhandled error has occurred.
        <a href="." class="reload">Reload</a>
        <span class="dismiss">&#10005;</span>
    </div>
</div>

@code {
    protected override void OnInitialized() => SettingsVm.Changed += OnChanged;

    private void OnChanged() => InvokeAsync(StateHasChanged);

    public void Dispose() => SettingsVm.Changed -= OnChanged;
}
```

- [ ] **Step 2: Create `src/SharpClient.UI/Layout/MainLayout.razor.css`**

```css
/* Layout is intentionally minimal — all styles live in wwwroot/app.css */
```

- [ ] **Step 3: Update `src/SharpClient.UI/_Imports.razor`**

Replace the entire file:

```razor
@using Microsoft.AspNetCore.Components.Web
@using Microsoft.AspNetCore.Components.Routing
@using SharpClient.Core.Presentation
@using SharpClient.UI.Components
@using SharpClient.UI.Layout
```

- [ ] **Step 4: Update `src/SharpClient.Web/Components/_Imports.razor`**

Change `@using SharpClient.Web.Components.Layout` to `@using SharpClient.UI.Layout`. Full file after change:

```razor
@using Microsoft.AspNetCore.Components.Routing
@using Microsoft.AspNetCore.Components.Web
@using static Microsoft.AspNetCore.Components.Web.RenderMode
@using SharpClient.Web
@using SharpClient.Web.Components
@using SharpClient.UI.Layout
```

- [ ] **Step 5: Update `src/SharpClient.App/Components/_Imports.razor`**

Change `@using SharpClient.App.Components.Layout` to `@using SharpClient.UI.Layout`. Full file after change:

```razor
@using System.Net.Http
@using System.Net.Http.Json
@using Microsoft.AspNetCore.Components.Forms
@using Microsoft.AspNetCore.Components.Routing
@using Microsoft.AspNetCore.Components.Web
@using Microsoft.AspNetCore.Components.Web.Virtualization
@using Microsoft.JSInterop
@using SharpClient.App
@using SharpClient.App.Components
@using SharpClient.UI.Layout
@using SharpClient.Core.Presentation
@using SharpClient.Core.Sessions
@using SharpClient.Core.Domain
@using SharpClient.UI.Components
```

- [ ] **Step 6: Delete per-host layout files**

```bash
rm src/SharpClient.Web/Components/Layout/MainLayout.razor
rm src/SharpClient.Web/Components/Layout/MainLayout.razor.css
rm src/SharpClient.App/Components/Layout/MainLayout.razor
rm src/SharpClient.App/Components/Layout/MainLayout.razor.css
```

- [ ] **Step 7: Build to verify**

```bash
dotnet build src/SharpClient.Web/SharpClient.Web.csproj
dotnet build src/SharpClient.App/SharpClient.App.csproj -f net10.0-android
```

Expected: 0 errors, 0 warnings.

- [ ] **Step 8: Run all tests**

```bash
dotnet run --project tests/SharpClient.Tests/SharpClient.Tests.csproj
dotnet run --project tests/SharpClient.UI.Tests/SharpClient.UI.Tests.csproj
dotnet run --project tests/SharpClient.Data.Tests/SharpClient.Data.Tests.csproj
```

Expected: 171 tests green.

- [ ] **Step 9: Commit**

```bash
git add src/SharpClient.UI/Layout/MainLayout.razor \
        src/SharpClient.UI/Layout/MainLayout.razor.css \
        src/SharpClient.UI/_Imports.razor \
        src/SharpClient.Web/Components/_Imports.razor \
        src/SharpClient.App/Components/_Imports.razor
git rm src/SharpClient.Web/Components/Layout/MainLayout.razor \
       src/SharpClient.Web/Components/Layout/MainLayout.razor.css \
       src/SharpClient.App/Components/Layout/MainLayout.razor \
       src/SharpClient.App/Components/Layout/MainLayout.razor.css
git commit -m "$(cat <<'EOF'
refactor: move MainLayout to SharpClient.UI; reorder nav to Worlds/Session/Settings

Single shared layout. Both hosts import SharpClient.UI.Layout and discover
MainLayout via _Imports.razor. Nav order updated: Worlds (/) is now first.

Co-Authored-By: Claude Sonnet 4.6 <noreply@anthropic.com>
EOF
)"
```

---

## Task 5: Move Routable Pages to SharpClient.UI

**Files:**
- Create: `src/SharpClient.UI/Pages/SessionPage.razor`
- Create: `src/SharpClient.UI/Pages/WorldsPage.razor`
- Create: `src/SharpClient.UI/Pages/SettingsPage.razor`
- Create: `src/SharpClient.UI/Pages/RulesPage.razor`
- Delete: `src/SharpClient.Web/Components/Pages/Home.razor`
- Delete: `src/SharpClient.Web/Components/Pages/Worlds.razor`
- Delete: `src/SharpClient.Web/Components/Pages/Settings.razor`
- Delete: `src/SharpClient.Web/Components/Pages/Rules.razor`
- Delete: `src/SharpClient.App/Components/Pages/Home.razor`
- Delete: `src/SharpClient.App/Components/Pages/Worlds.razor`
- Delete: `src/SharpClient.App/Components/Pages/Settings.razor`

**Interfaces:**
- Produces: four routable pages in `SharpClient.UI.Pages` namespace, each discovered by the Blazor router via `AdditionalAssemblies` (wired in Task 6). No `@rendermode` on any page — render mode is applied at the host boundary.
- Consumes: `SettingsViewModel`, `SessionsViewModel`, `ProtocolPanelViewModel`, `WorldManagerViewModel`, `TriggerAliasEditorViewModel` (all from `SharpClient.Core.Presentation` — already in scope via `SharpClient.UI/_Imports.razor`). `SessionScreen`, `WorldManager`, `SettingsView`, `TriggerEditor` components (all from `SharpClient.UI.Components` — already in scope).

- [ ] **Step 1: Create `src/SharpClient.UI/Pages/SessionPage.razor`**

Route is `/session` (not `/`). No `@rendermode`. All `@using` directives are already in `SharpClient.UI/_Imports.razor`.

```razor
@page "/session"

@inject SessionsViewModel Vm
@inject ProtocolPanelViewModel ProtocolVm

<PageTitle>SharpClient</PageTitle>

<SessionScreen Vm="@Vm" ProtocolVm="@ProtocolVm" />
```

- [ ] **Step 2: Create `src/SharpClient.UI/Pages/WorldsPage.razor`**

Worlds is the new landing page (`@page "/"`). Also keeps `/worlds` route. Injects `NavigationManager` (available in both hosts via `Microsoft.AspNetCore.Components` — no extra using needed). Adds `OnOpenRules` navigation callback that was missing from the App.

```razor
@page "/"
@page "/worlds"

@inject WorldManagerViewModel Vm
@inject NavigationManager Nav

<PageTitle>SharpClient &middot; Worlds</PageTitle>

<WorldManager Vm="@Vm" OnOpenRules="NavigateToRules" />

@code {
    protected override async Task OnInitializedAsync() => await Vm.LoadAsync();

    private void NavigateToRules(Guid worldId) =>
        Nav.NavigateTo($"/worlds/{worldId}/rules");
}
```

- [ ] **Step 3: Create `src/SharpClient.UI/Pages/SettingsPage.razor`**

```razor
@page "/settings"

@inject SettingsViewModel Vm

<PageTitle>SharpClient &middot; Settings</PageTitle>

<SettingsView Vm="@Vm" />
```

- [ ] **Step 4: Create `src/SharpClient.UI/Pages/RulesPage.razor`**

```razor
@page "/worlds/{WorldId:guid}/rules"

@inject TriggerAliasEditorViewModel Vm

<PageTitle>SharpClient &middot; Rules</PageTitle>

<div class="terminal-page">
    <div class="page-title">Triggers &amp; Aliases</div>
    <TriggerEditor Vm="@Vm" />
</div>

@code {
    [Parameter] public Guid WorldId { get; set; }

    protected override async Task OnInitializedAsync() => await Vm.LoadAsync(WorldId);
}
```

- [ ] **Step 5: Delete per-host page files**

```bash
rm src/SharpClient.Web/Components/Pages/Home.razor
rm src/SharpClient.Web/Components/Pages/Worlds.razor
rm src/SharpClient.Web/Components/Pages/Settings.razor
rm src/SharpClient.Web/Components/Pages/Rules.razor
rm src/SharpClient.App/Components/Pages/Home.razor
rm src/SharpClient.App/Components/Pages/Worlds.razor
rm src/SharpClient.App/Components/Pages/Settings.razor
```

- [ ] **Step 6: Build (will fail until Task 6 wires the router — that's expected; confirm error is only "no routes found")**

Do NOT run the full build yet. Just confirm the RCL itself compiles:

```bash
dotnet build src/SharpClient.UI/SharpClient.UI.csproj
```

Expected: 0 errors.

- [ ] **Step 7: Commit**

```bash
git add src/SharpClient.UI/Pages/SessionPage.razor \
        src/SharpClient.UI/Pages/WorldsPage.razor \
        src/SharpClient.UI/Pages/SettingsPage.razor \
        src/SharpClient.UI/Pages/RulesPage.razor
git rm src/SharpClient.Web/Components/Pages/Home.razor \
       src/SharpClient.Web/Components/Pages/Worlds.razor \
       src/SharpClient.Web/Components/Pages/Settings.razor \
       src/SharpClient.Web/Components/Pages/Rules.razor \
       src/SharpClient.App/Components/Pages/Home.razor \
       src/SharpClient.App/Components/Pages/Worlds.razor \
       src/SharpClient.App/Components/Pages/Settings.razor
git commit -m "$(cat <<'EOF'
refactor: move all routable pages to SharpClient.UI/Pages

SessionPage (@page /session), WorldsPage (@page / and /worlds),
SettingsPage (@page /settings), RulesPage (@page /worlds/{id}/rules).
No @rendermode directives — set at host boundary. WorldsPage gains
NavigationManager + OnOpenRules callback (was missing from App).

Co-Authored-By: Claude Sonnet 4.6 <noreply@anthropic.com>
EOF
)"
```

---

## Task 6: Wire Host Routers + Verify End-to-End

**Files:**
- Modify: `src/SharpClient.Web/Components/Routes.razor`
- Modify: `src/SharpClient.App/Components/Routes.razor`

**Interfaces:**
- Consumes: `SharpClient.UI.Components.SessionScreen` (used as the assembly anchor for `AdditionalAssemblies`); `SharpClient.UI.Layout.MainLayout` (imported via `_Imports.razor` updated in Task 4, accessible as `typeof(MainLayout)`).

- [ ] **Step 1: Update `src/SharpClient.Web/Components/Routes.razor`**

Add `AdditionalAssemblies` pointing to the RCL and update `DefaultLayout` to use `typeof(MainLayout)` (resolves to `SharpClient.UI.Layout.MainLayout` via the `_Imports.razor` using from Task 4).

```razor
<Router AppAssembly="typeof(Program).Assembly"
        AdditionalAssemblies="new[] { typeof(SharpClient.UI.Components.SessionScreen).Assembly }"
        NotFoundPage="typeof(Pages.NotFound)">
    <Found Context="routeData">
        <RouteView RouteData="routeData" DefaultLayout="typeof(MainLayout)" />
        <FocusOnNavigate RouteData="routeData" Selector="h1" />
    </Found>
</Router>
```

- [ ] **Step 2: Update `src/SharpClient.App/Components/Routes.razor`**

Same `AdditionalAssemblies`; no render mode on this host.

```razor
<Router AppAssembly="typeof(MauiProgram).Assembly"
        AdditionalAssemblies="new[] { typeof(SharpClient.UI.Components.SessionScreen).Assembly }"
        NotFoundPage="typeof(Pages.NotFound)">
    <Found Context="routeData">
        <RouteView RouteData="routeData" DefaultLayout="typeof(MainLayout)" />
        <FocusOnNavigate RouteData="routeData" Selector="h1" />
    </Found>
</Router>
```

- [ ] **Step 3: Build both hosts — must be 0 warnings**

```bash
dotnet build src/SharpClient.Web/SharpClient.Web.csproj
dotnet build src/SharpClient.App/SharpClient.App.csproj -f net10.0-android
```

Expected: 0 errors, 0 warnings for both.

- [ ] **Step 4: Run all three test suites**

```bash
dotnet run --project tests/SharpClient.Tests/SharpClient.Tests.csproj
dotnet run --project tests/SharpClient.UI.Tests/SharpClient.UI.Tests.csproj
dotnet run --project tests/SharpClient.Data.Tests/SharpClient.Data.Tests.csproj
```

Expected: 171 (or 172 if the new test was counted separately) tests green, 0 failures.

- [ ] **Step 5: Run the web host and verify runtime behaviour**

```bash
ASPNETCORE_URLS=http://localhost:5223 dotnet run --project src/SharpClient.Web/SharpClient.Web.csproj &
sleep 5   # wait for "Now listening" in output
curl -s http://localhost:5223/
curl -s http://localhost:5223/session
pkill -f SharpClient.Web
```

Verify:
- `curl http://localhost:5223/` response: contains "Worlds" (the WorldManager component's title) or "No worlds yet"; does NOT contain "Vesper", "Sindome", "Drome", "Thorne", "Doran".
- `curl http://localhost:5223/session` response: contains the session screen empty state (no sessions seeded).

- [ ] **Step 6: Write the consolidation report**

Create `/home/grave/RiderProjects/SharpClient/.superpowers/sdd/web-consolidation-report.md` documenting:
- What moved where
- Files deleted
- Per-host wiring differences (AdditionalAssemblies, render mode, CSS path)
- The curl evidence (paste the responses)

- [ ] **Step 7: Final commit**

```bash
git add src/SharpClient.Web/Components/Routes.razor \
        src/SharpClient.App/Components/Routes.razor \
        .superpowers/sdd/web-consolidation-report.md
git commit -m "$(cat <<'EOF'
feat: wire AdditionalAssemblies in both hosts; RCL pages now routable

Web Routes.razor: AdditionalAssemblies includes SharpClient.UI assembly;
DefaultLayout -> typeof(MainLayout) from SharpClient.UI.Layout.
App Routes.razor: same AdditionalAssemblies; no render mode (Hybrid).
Both builds clean (0 warnings). / now serves Worlds page with no demo data.

Co-Authored-By: Claude Sonnet 4.6 <noreply@anthropic.com>
EOF
)"
```

---

## Self-Review Checklist

**Spec coverage:**
- [x] RCL pages in `SharpClient.UI/Pages/` with correct routes — Task 5
- [x] No `@rendermode` on shared pages — explicit in all page files
- [x] Worlds is landing page (`@page "/"`) — WorldsPage.razor
- [x] `@rendermode InteractiveServer` at `<Routes>` level in Web's `App.razor` — Task 3 Step 2
- [x] `AdditionalAssemblies` in both hosts' routers — Task 6
- [x] Layout moved to `SharpClient.UI/Layout/` — Task 4
- [x] CSS moved to `SharpClient.UI/wwwroot/app.css` — Task 3
- [x] Web `App.razor` references `_content/SharpClient.UI/app.css` — Task 3 Step 2
- [x] App `index.html` references `_content/SharpClient.UI/app.css` — Task 3 Step 3
- [x] Core `TelnetSessionLauncher` — Task 1 Step 1
- [x] Demo sessions removed from Web — Task 1 Step 3 (Program.cs rewrite)
- [x] `DemoSessionLauncher.cs` and `DemoSession.cs` deleted — Task 1 Step 5
- [x] `TelnetNegotiationCore` added to Web.csproj — Task 1 Step 2
- [x] App's `MauiProgram.cs` uses Core launcher — Task 1 Step 4
- [x] Live update subscriptions in `SessionsViewModel` — Task 2
- [x] TUnit test for `LineAppended` → `Changed` — Task 2 Step 1
- [x] Nav order: Worlds / Session / Settings — Task 4 Step 1 (MainLayout)
- [x] `Error.razor` and `NotFound.razor` stay in Web — not touched
- [x] `ReconnectModal` stays in Web — not touched
- [x] App's `NotFound.razor` stays in App — not touched
- [x] 171 tests verified after each commit — Steps 4 and 4 of Tasks 4 and 6
- [x] 0 warnings in both builds — verified in Tasks 1, 3, 4, 6
- [x] curl evidence from runtime — Task 6 Step 5

**Placeholder scan:** No TBDs, no "implement later", no "handle edge cases" — all code is fully written out.

**Type consistency:**
- `FakeSession.Append(ScrollbackLine line)` — defined in the existing `FakeSession.cs`; used in the new test as `session.Append(new ScrollbackLine([]))`. ✓
- `NavigationManager` injected in `WorldsPage.razor` — available via `Microsoft.AspNetCore.Components` (already in `Microsoft.AspNetCore.Components.Web` using). ✓
- `typeof(MainLayout)` in both `Routes.razor` files resolves to `SharpClient.UI.Layout.MainLayout` via the `@using SharpClient.UI.Layout` in each host's `_Imports.razor`. ✓
- `typeof(SharpClient.UI.Components.SessionScreen).Assembly` — `SessionScreen` exists in `src/SharpClient.UI/Components/SessionScreen.razor`. ✓
