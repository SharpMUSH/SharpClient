# Compose tab and post-crash diagnostics — design

Date: 2026-08-10

Two independent features for SharpClient:

1. **Compose** — a dedicated screen for writing multi-line MUSH poses, with a command
   prefix and automatic escaping, sent to the active session as a single line.
2. **Diagnostics** — an in-app viewer for the on-device log, plus a "crashed last run"
   banner, so a crash can be investigated after the client is restarted.

They share no code and can be built in either order.

---

## Feature 1 — Compose

### Problem

Poses are multi-line prose. The client's input bar is a single-line field, and the wire
protocol needs one line: newlines have to become `%r`, and a literal `%` in the prose has
to become `%%` or the server substitutes it. Doing this by hand is error-prone, so poses
get written elsewhere and pasted in mangled.

### Formatter

`SharpClient.Core/Formatting/MushPoseFormatter.cs` — a pure static class, no dependencies,
the whole of the escaping contract in one testable place.

```csharp
public static string Format(string prefix, string body)
```

Steps, in this order:

1. Normalise line endings: `\r\n` and lone `\r` become `\n`.
2. Escape `%` → `%%`.
3. Trim trailing whitespace from each line; drop trailing blank lines. Interior blank
   lines are preserved and become consecutive `%r`s.
4. Replace `\n` → `%r`. This runs **after** step 2 so the inserted `%r` markers are not
   themselves escaped.
5. Join prefix and body with the separator rule below.

Separator rule:

- A built-in prefix (`say`, `pose`, `semipose`, `@emit`) is followed by one space.
- A custom prefix ending in `=`, `/`, or whitespace is joined verbatim, so `page Bob=`
  yields `page Bob=He grins…`.
- Any other custom prefix gets one space.

Nothing else is escaped. `[`, `]`, `\`, `;` and `,` pass through unchanged and remain
parser syntax on the server.

**Accepted consequence:** a user who deliberately types `%r`, `%t`, or another
substitution gets it neutered to `%%r`. This is the direct cost of literal `%` escaping.
The Preview mode shows the exact wire text, so the result is visible before sending rather
than surprising afterwards. There is no raw / no-escape mode in this design.

### View model

`SharpClient.Core/Presentation/ComposeViewModel.cs` — singleton, constructor dependencies
`ISessionManager` and `IPreferences`, matching the shape of the existing
`SessionsViewModel` (public properties, a `Changed` event, no INotifyPropertyChanged).

| Member | Behaviour |
| --- | --- |
| `SelectedPrefix` | `PosePrefix` enum: `Say`, `Pose`, `Semipose`, `Emit`, `Custom`. |
| `CustomPrefix` | Free text. Persisted per world through `IPreferences`, key `compose.custom.{ISession.WorldId}`. Loaded when the active session changes. |
| `Body` | The draft text for the active session. |
| `Preview` | `MushPoseFormatter.Format(EffectivePrefix, Body)`. |
| `CanSend` | Active session is `Connected`, `Body` is not blank, and when `SelectedPrefix == Custom` the custom text is not blank. |
| `SendAsync()` | Sends `Preview` on the active session, clears that session's draft, raises `Changed`. |
| `Changed` | Raised on property changes and on session-manager / active-state changes, so `CanSend` re-evaluates when a connection comes back. |

Drafts live in a `Dictionary<ISession, string>` and are pruned when a session closes —
the same pattern `SessionsViewModel` already uses for command history. Switching session
tabs mid-pose does not clobber the draft.

Registered in `SharpClient.UI/ServiceCollectionExtensions.cs` as a singleton so both hosts
get it from one place.

### UI

`SharpClient.UI/Components/ComposeView.razor` plus `Pages/ComposePage.razor` at route
`/compose`, with a fifth nav entry in `MainLayout` between Session and History.

The view fills the whole tab body — `.sc-content` is a flex column at full height:

- **Top, fixed height:** the prefix chip row — `say`, `pose`, `semipose`, `@emit`,
  `custom`. Selecting `custom` reveals a single-line text field beneath the chips.
- **Middle, `flex: 1`:** in Edit mode a monospace `<textarea>` filling the region; in
  Preview mode a read-only monospace pane of the exact wire text, wrapped and selectable,
  occupying the same region at the same size so toggling does not reflow the page.
- **Bottom, fixed height:** character count of the formatted line, and the buttons
  `Clear`, `Preview` / `Edit` (one button toggling label and mode), and `Send`.

`Send` is enabled per `CanSend` and works from either mode. Ctrl+Enter and Cmd+Enter send;
plain Enter inserts a newline. When no session is connected, `Send` is disabled and the
footer shows a "no connected session" hint linking to `/session`.

Styling uses the existing `sc-` class conventions and the theme CSS variables already
emitted by `SettingsViewModel.RootStyleVariables`, so accent, glow, and scanline settings
apply without new theming code.

---

## Feature 2 — Diagnostics

### Problem

The write side already exists: `FileLogStore` appends timestamped entries to a rotated
file under the app's private data directory, `FileLoggerProvider` routes `ILogger` output
into it, and `AppDomain.UnhandledException`, `TaskScheduler.UnobservedTaskException` and
`AndroidEnvironment.UnhandledExceptionRaiser` all write crashes there. The only way to
read any of it is Settings → Diagnostics → Export log, which opens the OS share sheet.
There is no way to look at a crash on the device where it happened.

### Refactor: move the log store into Core

`FileLogStore` and `FileLoggerProvider` move from `SharpClient.App/Services/` to
`SharpClient.Core/Diagnostics/`. `FileLogStore` takes its log directory as a constructor
argument instead of reading `FileSystem.AppDataDirectory` itself; the MAUI host supplies
that path at registration time. `SharpClient.Core` gains a
`Microsoft.Extensions.Logging.Abstractions` package reference.

This is a prerequisite, not a side quest. Both types currently live in the MAUI project,
which no test project can reference — the write format and the rotation logic have no
coverage at all today. After the move the writer, the parser, and the reader are all
unit-testable against a temp directory, and write→parse round-tripping can be asserted
directly.

`MauiLogExporter` stays in `SharpClient.App`; the share sheet is platform code.

### Reader

`SharpClient.Core/Diagnostics/ILogReader.cs`, sitting alongside the existing
`ILogExporter`, with a `NoopLogReader` for the Web host exactly as `NoopLogExporter` does.

```csharp
public interface ILogReader
{
    bool IsAvailable { get; }
    Task<IReadOnlyList<LogEntry>> ReadAsync(int maxEntries = 500);
    Task<CrashReport?> GetPendingCrashAsync();
    Task DismissCrashAsync();
    Task ClearAsync();
}

public sealed record LogEntry(
    DateTimeOffset Timestamp, string Level, string Category, string Message, string? Detail);

public sealed record CrashReport(
    DateTimeOffset Timestamp, string Source, string Message, string Detail);
```

`ReadAsync` merges the rotated backup (`sharpclient.log.1`) and the current file into one
chronological sequence and returns the newest `maxEntries` entries, newest first.
`ClearAsync` deletes the current log and its rotated backup; it leaves `last-crash.txt`
alone, since dismissing a crash banner is a separate action.

The MAUI host registers `FileLogReader` as `ILogReader` over the same `FileLogStore`
singleton it already registers for `ILogExporter`; the Web host registers `NoopLogReader`
beside its existing `NoopLogExporter`.

`LogEntryParser` is a pure static class over raw text. A line matching the header pattern
that `FileLogStore.Append` writes — timestamp, `[LEVEL]`, optional `category: `, message —
starts a new entry; every following line that does not match accumulates into that entry's
`Detail`, which is how a stack trace stays attached to its exception. Text before the first
recognised header is discarded (it is the tail of a rotated-away entry).

### Crash marker

`FileLogStore.WriteException` additionally writes `last-crash.txt` into the log directory,
containing the same formatted block it appended to the log. `GetPendingCrashAsync` reads
that file; `DismissCrashAsync` deletes it. Using a sidecar rather than scanning the log
means the check is cheap at startup and survives rotation. Writing it is inside the same
never-throw guard as the log append.

### UI

`SharpClient.UI/Components/DiagnosticsView.razor` plus `Pages/DiagnosticsPage.razor` at
route `/diagnostics`. It is not a nav tab: Settings → Diagnostics gains a **View log**
button next to the existing **Export log**, and the whole section stays hidden when
`ILogReader.IsAvailable` is false.

- Filter chips: **All**, **Errors** (`Error`, `Critical`, `CRASH`), **Crashes** (`CRASH`).
  The route accepts `?filter=crashes` so the banner can deep-link.
- Each entry renders as timestamp, a level pill, category, and message, with `Detail` in a
  collapsed `<details>` element.
- Actions: `Refresh`, `Copy` (clipboard via the existing `sc-interop.js` module),
  `Share` (delegates to `ILogExporter`), and `Clear` behind a confirm step.
- Newest first, capped at 500 rendered entries with a note when older entries were
  truncated. Empty state: "No log entries recorded yet."

`SharpClient.UI/Components/CrashBanner.razor` renders in `MainLayout` above `@Body`,
visible only when `GetPendingCrashAsync` returns a report: *"SharpClient crashed last run
(timestamp)"* with **View** and a dismiss **×**. View navigates to
`/diagnostics?filter=crashes`; dismiss calls `DismissCrashAsync`. The report is fetched in
`OnAfterRenderAsync(firstRender)` so a slow or failed file read never blocks first paint.

### Out of scope

- **Hard kills.** An OOM kill or a native crash never reaches the managed hooks, so a run
  ending that way leaves no crash marker and shows no banner. Detecting it would need a
  clean-shutdown sentinel written on every normal exit; that is deliberately not in this
  design.
- **Web host.** `NoopLogReader`, no viewer, no banner. The Web build has no persistent log.

---

## Testing

TUnit in `tests/SharpClient.Tests`, bUnit in `tests/SharpClient.UI.Tests`, following the
existing fakes in both projects.

**Formatter** — `%` escaping; escape-before-`%r` ordering; `\r\n`, `\r`, and `\n` all
normalising identically; trailing whitespace and trailing blank lines dropped; interior
blank lines preserved as consecutive `%r`; one separator case per branch (built-in, custom
ending in `=`, custom ending in `/`, custom ending in whitespace, bare custom); empty and
whitespace-only body.

**ComposeViewModel** — `CanSend` false when disconnected, when the body is blank, and when
`Custom` is selected with no custom text; `SendAsync` sends the formatted line and clears
only that session's draft; drafts survive switching sessions and are pruned on close;
custom prefix round-trips through `IPreferences` per `WorldId`; `Changed` fires on
connection-state transitions.

**Log store and reader** — append and read back through the parser in a temp directory;
rotation at the size threshold preserving the backup; a rotation failure falling through
to a plain append; multi-line exception detail staying attached to its entry; malformed
and truncated leading lines discarded; `ReadAsync` ordering across the rotation boundary;
`maxEntries` truncation keeping the newest; crash marker written on `WriteException`, read
by `GetPendingCrashAsync`, gone after `DismissCrashAsync`; `ClearAsync` removing both
files.

**bUnit** — prefix chips change the previewed text; Preview/Edit toggles the middle region
without losing the draft; `Send` invokes the session and clears the editor; `Send` disabled
with the hint shown when no session is connected; diagnostics filter chips narrow the list;
diagnostics empty state; crash banner renders for a pending report and disappears after
dismiss.
