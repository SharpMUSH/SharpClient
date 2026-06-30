# SharpClient — Design Spec

**Date:** 2026-06-29
**Status:** Approved (brainstorming) → ready for implementation planning
**Location:** `/home/grave/RiderProjects/SharpClient/`

## 1. Summary

SharpClient is a **.NET MAUI Blazor Hybrid** client for connecting to text-based
MUSH/MUD servers over telnet. It is **Android-primary** (phone + tablet), with the
desktop target kept buildable for a fast development loop. The UI is Razor/Blazor
rendered in a `BlazorWebView`; all networking and logic run as native C# on-device,
so it can open raw TCP sockets (which a browser WASM sandbox cannot).

Telnet protocol handling uses **TelnetNegotiationCore (TNC) 2.5.0** consumed from
**NuGet** (not a project reference), running on-device against a `TcpClient` stream.

## 2. Goals (v1, "rich from the start")

- Manage **Worlds** (a MUSH: name/host/port) each containing **Characters**.
- Connect a Character → opens a **Session**; multiple Sessions run as **tabs**.
- Stream incoming text into a virtualized scrollback view with **ANSI + xterm-256
  color** rendering.
- Command input with history and alias expansion on send.
- **Declarative** triggers and aliases (regex/substring → actions; no scripting engine).
- Telnet **negotiation surfaced** for debugging via a collapsible **Protocol Panel**
  (GMCP / MSDP / NAWS).
- Per-Character **session logging** to app data.

### Non-goals (v1)

- Scripting engine (Lua/C#) for triggers — declarative rules only. (Possible v2.)
- iOS/Mac build targets (architecture stays portable; not a v1 deliverable).
- Custom HUD widgets driven by GMCP/MSDP — v1 surfaces this data in the Protocol
  Panel only.

## 3. Architecture

Single MAUI Blazor Hybrid app. Android is the primary deployment target; the
desktop target (Windows, or Mac Catalyst on macOS hosts) is kept buildable purely
to shorten the inner dev loop, since Android SDK/emulator setup is heavier.

```
TcpClient stream ──► TNC Interpreter ──► TelnetConnection (events)
                                              │
                                              ▼
                          Session (scrollback + logger + trigger engine)
                                              │
                                              ▼
                       Blazor UI (OutputView / InputBar / Tabs / ProtocolPanel)
```

### Rendering approach (decided: A)

Incoming text is parsed in C# into a list of `StyledSegment`s (text + style), which
Razor renders as `<span>`s in a virtualized scroll view. Everything stays in
testable C#; declarative triggers/highlighting operate over the same segment model.
Rejected alternatives: ANSI→HTML string via `MarkupString` (escaping/injection risk),
and `xterm.js` JS interop (adds a JS dependency and splits trigger/selection logic
across the JS↔C# boundary).

## 4. Data model

- **World** — a MUSH definition: `Name`, `Host`, `Port`, world-level defaults
  (protocol prefs, shared triggers/aliases). Contains Characters.
- **Character** — belongs to a World: `Name`, login automation (connect string such
  as `connect <name> <secret>`), character-level triggers/aliases that merge over
  the world-level set (character overrides world).
- **Session** — a live connection for exactly one Character. Connecting a Character
  opens a Session; multiple Sessions are the tabs. A given Character has at most one
  live Session at a time; different Characters each get their own tab.

### Persistence

- Worlds + Characters serialized to `worlds.json` in `FileSystem.AppDataDirectory`.
- **Secrets** (passwords in connect strings) stored via MAUI `SecureStorage`, keyed
  by Character — never written to `worlds.json` in plaintext.
- Per-Character session logs written under app data (`logs/<world>/<character>/…`).

## 5. Components (each a focused, independently testable unit)

| Unit | Responsibility | Depends on |
|------|----------------|------------|
| `TelnetConnection` | Wrap TNC `Interpreter` + `TcpClient`; own the read loop; expose `ConnectAsync`/`SendAsync`/`Disconnect` and events `LineReceived`, `StateChanged`, `GmcpReceived`, `MsdpReceived`. | TNC, `System.Net.Sockets` |
| `AnsiParser` | Pure: raw line → `List<StyledSegment>` (fg/bg/bold/underline/inverse, incl. xterm-256). No I/O. | — |
| `TriggerEngine` | Apply declarative trigger rules to incoming lines → actions (highlight/send/notify). | `AnsiParser` model |
| `AliasEngine` | Expand input patterns → text with `$1…$n` args before send. | — |
| `WorldStore` | CRUD Worlds with nested Characters; JSON persistence + SecureStorage for secrets. | MAUI storage |
| `SessionLogger` | Append per-Character session output (raw + cleaned) to a log file. | file I/O |
| `Session` | Tie one Character (+ its World) to a live `TelnetConnection` + logger + trigger engine + scrollback buffer of parsed lines. The unit the UI binds to. | above units |
| `SessionManager` | Hold open Sessions (tabs); track the active one. | `Session` |
| Razor UI | `WorldManager` (CRUD Worlds/Characters, Connect), `SessionTabs`, `OutputView` (virtualized segments), `InputBar` (history + alias expansion), `ProtocolPanel` (live GMCP/MSDP/NAWS), trigger/alias editor. | services above |

## 6. Telnet negotiation surfacing

`TelnetConnection` routes TNC negotiation callbacks (GMCP, MSDP, NAWS, and general
option state) out as events. `Session` records the latest values; `ProtocolPanel`
renders them as labeled key/value/JSON for debugging. NAWS reports the current
terminal size derived from the output view dimensions.

## 7. Triggers & aliases (declarative)

- **Trigger:** `match` (regex or substring) on an incoming line → one or more
  actions: `highlight` (style the matched segment), `send` (queue text to server),
  `notify` (local notification/toast). Stored as JSON data.
- **Alias:** input `pattern` → `expansion` template using `$1…$n` capture args,
  applied by `AliasEngine` when the user sends a line.
- Rules exist at **world** and **character** scope; character rules merge over world
  rules (character wins on conflict).

## 8. Testing

- **`SharpClient.Tests`** using **TUnit** (not xUnit).
- Covered: `AnsiParser` (SGR incl. xterm-256, malformed sequences), `TriggerEngine`
  and `AliasEngine` (match/expand/merge precedence), `WorldStore` (round-trip,
  secret separation), and trigger/alias world↔character merge logic.
- `TelnetConnection` exercised against a loopback echo server / TNC's negotiation
  patterns.

## 9. Build / environment notes

- SDK: `dotnet 11.0.100-preview.3` is installed. The **`maui` workload** is not yet
  installed — required, installed during setup.
- TNC 2.5.0 targets `net10.0` / `net8.0` / `netstandard2.0`; consumed from NuGet.
- Building/deploying the Android APK requires the Android SDK + emulator/device;
  the desktop target is used for the day-to-day dev loop until that is set up.

## 10. Suggested phasing (single spec, phased plan)

1. **Core pipeline** — `TelnetConnection` + `AnsiParser` + minimal `Session`,
   connect/send/receive to a real MUSH, plain text.
2. **UI shell + color** — `OutputView` (virtualized, colored), `InputBar` with
   history, `SessionTabs`, `SessionManager`.
3. **Worlds & Characters** — `WorldStore`, `WorldManager`, SecureStorage secrets,
   Connect flow + login automation.
4. **Negotiation + Protocol Panel** — GMCP/MSDP/NAWS surfacing.
5. **Triggers/aliases + logging** — declarative engines, editor UI, `SessionLogger`.

Visual/interaction design (overall direction, color system coexisting with raw
ANSI, typography, tab UX, component states) is handled separately via the
frontend-design brief and folds into phases 2–5.
