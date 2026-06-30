# MAUI Integration Report — SharpClient.App

## Services Added (`src/SharpClient.App/Services/`)

| File | Interface | Notes |
|------|-----------|-------|
| `MauiAppStorage.cs` | `IAppStorage` | Returns `FileSystem.AppDataDirectory/sharpclient.db` |
| `MauiSecretStore.cs` | `ISecretStore` | Delegates to `Microsoft.Maui.Storage.SecureStorage.Default` (Android Keystore) |
| `MauiNotifier.cs` | `INotifier` | **Stub** — logs via `ILogger<MauiNotifier>` using `[LoggerMessage]` source-gen; push/local-notification support deferred |
| `TelnetSessionLauncher.cs` | `ISessionLauncher` | Uses `ITelnetConnectionFactory` (Core abstraction, see below) to open a TCP telnet connection, wraps it in `Session`, auto-sends the connect string from `ISecretStore` |

## Core Additions (`src/SharpClient.Core/Connection/`)

| File | Purpose |
|------|---------|
| `ITelnetConnectionFactory.cs` | Interface: `ITelnetConnection CreateConnection()` |
| `TelnetConnectionFactory.cs` | Wraps `ITelnetInterpreterFactory` from TNC; returns `new TelnetConnection(factory)` |

**Why the extra abstraction?**  
`ITelnetInterpreterFactory` (from TelnetNegotiationCore) resolves fine for `net10.0` but causes
`CS0246` under `net10.0-android`. Root cause: TNC's net10.0 DLL references `Stateless,
Version=4.0.0.0, PublicKeyToken=93038f0927583c9a`; the available `stateless` 5.20.0 package DLL
does not carry that public-key token, so Roslyn silently fails to resolve TNC's type metadata in
the Android compilation context. Placing the TNC-dependent class in Core (compiled for plain
`net10.0`) avoids this; App only references Core interfaces.

## DI Registrations (`MauiProgram.cs`)

```
AddTelnetClient()                                    // registers ITelnetInterpreterFactory
IAppStorage          → MauiAppStorage               singleton
ISecretStore         → MauiSecretStore              singleton
INotifier            → MauiNotifier                 singleton
AppDbContext                                         transient (SQLite, net10.0-android safe)
IWorldStore          → WorldStore                   transient (depends on DbContext)
ISessionHistory      → SessionHistory               transient (uses IAppStorage path directly)
SessionManager       (concrete + ISessionManager)   singleton
SessionsViewModel                                   singleton
ProtocolPanelViewModel                              singleton
WorldManagerViewModel                               transient
ISessionLauncher     → TelnetSessionLauncher        transient
ITelnetConnectionFactory → TelnetConnectionFactory  singleton
ITriggerEngine       → TriggerEngine                singleton (stateless)
IAliasEngine         → AliasEngine                  singleton (stateless)
```

Lifetimes follow the Web app pattern; `AppDbContext`/`IWorldStore`/`WorldManagerViewModel` are
transient rather than scoped because MAUI Blazor Hybrid has no HTTP request scope.

## UI Changes

- `Components/_Imports.razor` — added `SharpClient.Core.*` and `SharpClient.UI.Components` usings
- `Components/Pages/Home.razor` — renders `<SessionScreen Vm="@Vm" ProtocolVm="@ProtocolVm" />`
- `Components/Pages/Worlds.razor` — new page at `/worlds`, renders `<WorldManager Vm="@Vm" />`
- `Counter.razor`, `Weather.razor` — deleted
- `Components/Layout/MainLayout.razor` — replaced with sc-nav bar (Session / Worlds links), matches Web
- `wwwroot/app.css` — replaced with full design-token CSS from SharpClient.Web (dark theme, sc-* classes)
- `wwwroot/index.html` — added JetBrains Mono + Space Grotesk Google Fonts `<link>`s; removed unused bootstrap reference

## Android 0-Warning Build Notes

- `NuGetAuditSuppress` added for `GHSA-2m69-gcr7-jv3q` (SQLitePCLRaw 2.1.11 advisory, mirrors Data project)
- `TelnetNegotiationCore 2.5.0` added as direct `PackageReference` (required for `AddTelnetClient()` extension method; transitive exposure from Core does not flow to `net10.0-android`)
- `MauiNotifier` uses `[LoggerMessage]` source-generation to satisfy CA1848/CA1873 (TreatWarningsAsErrors=true)
- No broad `#pragma warning disable` or `SuppressTrimAnalysisWarnings` needed; Debug build does not run the linker

## Stubbed / Deferred

- **INotifier** → logs to ILogger only; replace `MauiNotifier` with a `Plugin.LocalNotification`-backed implementation when push/in-app notifications are needed
- **Android INTERNET permission** → `AndroidManifest.xml` not modified; add `<uses-permission android:name="android.permission.INTERNET" />` before first device run
- **EF Core migrations** → `AppDbContext.EnsureCreated()` is called lazily by `WorldStore`; no migration runner wired
- **Preferences/Settings screen** — not implemented in this integration
- **No demo/seed sessions** — app starts with the World Manager in its empty state

## Build Result

```
dotnet build src/SharpClient.App/SharpClient.App.csproj -f net10.0-android
  → Build succeeded.  0 Warning(s)  0 Error(s)

dotnet run --project tests/SharpClient.Tests/...
  → Passed!  total: 89  failed: 0  succeeded: 89

SharpClient.UI.Tests build: 0 warnings, 0 errors
SharpClient.Data.Tests build: 0 warnings, 0 errors
```
