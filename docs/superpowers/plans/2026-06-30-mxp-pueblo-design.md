# MXP / Pueblo Clickable Command-Links — Design & Implementation Plan

**Date:** 2026-06-30  
**Scope:** Research spike + design for v1 "clickable SEND/xch_cmd links" only.  
**No source files were modified to produce this document.**

---

## 1. TNC MXP Support — Evidence

**TNC fully supports MXP telnet negotiation.** Evidence:

| Item | Location | Detail |
|------|----------|--------|
| `Trigger.MXP = 91` | `TelnetNegotiationCore/Models/Trigger.cs:317` | Option 91 defined, refs Zugg spec |
| `State.DoMXP / WontMXP / WillMXP / DontMXP` | `Models/State.cs:103-107` | State machine nodes |
| `MXPProtocol` plugin | `Protocols/MXPProtocol.cs` | Client mode: sees `IAC WILL MXP` → sends `IAC DO MXP` → fires `_onMXPEnabled`. Server mode: sends `IAC WILL MXP` on build. |
| `AddDefaultMUDProtocols` includes MXP | `Builders/TelnetInterpreterBuilderExtensions.cs:86-88` | Plugin added by default; optional `onMXPEnabled` callback parameter |
| Fluent extension | `Builders/PluginConfigurationExtensions.cs:417-423` | `.OnMXPEnabled(callback)` |
| Unit tests | `UnitTests/MXPTests.cs` | 6 tests: server announce, client DO, enable/disable callbacks, IsMXPActive |

**Critical gap:** TNC handles the telnet option (IAC WILL/DO 91) only. It does **not** parse MXP markup. ESC[#z line-mode control sequences and `<SEND>` / `<A>` tags flow through in the decoded text string delivered to `OnSubmit` → `Session.OnLineReceived`. These are **application-layer markup**, not telnet protocol elements.

**Current behaviour in SharpClient:** `TelnetConnection.ConnectAsync` calls `AddDefaultMUDProtocols(onGMCPMessage:..., onMSSP:...)` — `onMXPEnabled` is implicitly null. **MXP negotiation already happens** (the plugin is wired), but the application is never notified. Wiring the callback is a one-liner.

---

## 2. TNC Pueblo Support — Evidence

**TNC has no Pueblo protocol support.** The only Pueblo reference in TNC is:

```
TelnetNegotiationCore/Models/MSSPConfig.cs:181
[Name("PUEBLO"), Official(false)]
public bool? Pueblo { get; set; }
```

This is an MSSP advertisement field only (for server browsers), not a protocol implementation.

**Pueblo negotiation is banner-based, not telnet-option-based.** The server emits a plain-text string containing `"This world is Pueblo"` (typically `"This world is Pueblo 1.10 enhanced.\r\n"` or similar) in the data stream. The client detects this, sets a `puebloActive` flag, and responds by sending `"PUEBLOCLIENT 2.01\r\n"` as a normal command. There is no telnet option to negotiate. TNC cannot help here — Pueblo detection must live at the `Session` layer, scanning incoming text lines.

---

## 3. MXP Minimum Specification for Clickable Links

Sources: Zugg MXP spec at zuggsoft.com/zmud/mxp.htm; MUSHclient wiki.

### 3.1 Telnet Negotiation

- Telnet option **91** (`0x5B`)
- Server sends `IAC WILL 91` → client responds `IAC DO 91` (client mode, which is what SharpClient uses as a MUD client)
- TNC `MXPProtocol` handles this already

### 3.2 MXP Line-Mode Control Sequences

After MXP negotiation, the server may precede each line with `ESC[#z` (CSI sequences):

| ESC[#z | Mode | Meaning |
|--------|------|---------|
| `ESC[0z` | Open | Default per-line mode; reverts to default at each newline. Only Open-category tags permitted. |
| `ESC[1z` | Secure | All MXP tags allowed, including SEND/A; reverts at newline. |
| `ESC[2z` | Locked | No MXP parsing; treat as verbatim text. |
| `ESC[3z` | Reset | Close open tags, reset mode to Open, clear formatting. |
| `ESC[5z` | Lock Open | Set Open as the persistent default (survives newlines). |
| `ESC[6z` | Lock Secure | Set Secure as persistent default. |

**Security rule:** `<SEND>` and `<A>` are "Secure" elements. They may only execute on a Secure line (mode 1 or Lock Secure / mode 6). On Open or Locked lines, tags must be stripped without producing clickable output. This is the entire security model of MXP and must be enforced by the client.

**Current AnsiParser behaviour:** The parser consumes all `ESC[...z` sequences silently (the `IsCsiFinal` check passes 'z' as a final byte in `[@..~]`; the `if (final == 'm')` branch is not taken, so the sequence is skipped). Mode bytes are lost. The parser must be extended to intercept 'z'-final sequences.

### 3.3 SEND Tag Formats

```xml
<!-- Primary form: href specifies the command sent, display text is separate -->
<SEND href="go north" hint="Move north">North</SEND>

<!-- Shorthand: content IS the command sent -->
<SEND>go north</SEND>

<!-- PROMPT variant: places command on input line instead of sending -->
<SEND href="go north" PROMPT>North</SEND>
```

- Attribute values may be quoted (`href="go north"`) or unquoted (`href=go_north`); quotes are required for multi-word values.
- Tags are case-insensitive: `<send>`, `<SEND>`, `<Send>` all valid.
- Closing `</SEND>` is required; the tag is not empty.
- `PROMPT` flag is optional; for v1 we can ignore it (treat as send).

### 3.4 A HREF Tag Format (for completeness)

```xml
<A href="http://example.com" hint="Open website">link text</A>
<A href="telnet://mud.example.com:4000">Connect to alternate</A>
```

These are for external URL/telnet links. For v1 scope (command-links), `<A>` with `http://` href can be rendered as a plain external link (or ignored). The MXP spec says `<A>` with a `telnet://` URL could spawn a new connection — out of scope for v1.

---

## 4. Pueblo Minimum Specification

Sources: zuggsoft.com/zmud/zmudpueblo.htm; sharpmush help; uecasm/pueblo GitHub.

### 4.1 Negotiation (Banner Detection)

1. Server sends a line containing `"This world is Pueblo"` (exact substring match; version number varies).
2. Client responds via the normal text channel: send the string `"PUEBLOCLIENT 2.01\r\n"`.
3. No telnet option. Detection must happen in `Session.OnLineReceived` before the line is parsed for markup.

### 4.2 Clickable Link Tag

```html
<a xch_cmd="go north" xch_hint="Move north">North</a>
<a xch_cmd=go_north>North</a>
```

- `xch_cmd` attribute: the command to send. Required for it to be a command-link.
- `xch_hint` attribute: mouseover text. Optional.
- Standard HTML `<a>` element; Pueblo-specific attributes are `xch_cmd` and `xch_hint`.
- No line-mode security model. Trust is inherent: Pueblo mode is only active after the server proved it can send the banner string, so the server controls all content anyway.
- Attribute values may be quoted or unquoted.

---

## 5. Design

### 5.1 Segment Model — Recommendation

**Add two optional `init` properties to `StyledSegment`.**

`StyledSegment` is currently `public readonly record struct StyledSegment(string Text, TextStyle Style)`.

Proposed change:

```csharp
public readonly record struct StyledSegment(string Text, TextStyle Style)
{
    /// <summary>
    /// If non-null, this segment is a clickable command-link. Clicking it sends this string
    /// to the server via ISession.SendAsync. Null for normal text segments.
    /// </summary>
    public string? Command { get; init; }

    /// <summary>
    /// Optional tooltip text for the command link. Null if not specified.
    /// </summary>
    public string? Hint { get; init; }
}
```

**Churn assessment:**

- The positional constructor `new StyledSegment(text, style)` is unchanged. All existing call sites (one in `AnsiParser.cs`, any in tests) continue to compile without modification.
- `SegmentStyle.ToCss(StyledSegment)` is unchanged — it only reads `Style`.
- `OutputView.razor` needs one conditional: `@if (segment.Command is not null)` renders a button; else the existing span.
- `SegmentStyleTests.cs` and `AnsiParserTests.cs` don't construct with the new properties and do not break.
- `record struct` equality includes all properties — existing equality comparisons still work; link segments will compare unequal to non-link segments of same text, which is correct.

**Rejected alternatives:**

- `LinkSegment` as a separate sealed record: `StyledSegment` is `readonly record struct`, which cannot be inherited in C#. Would require `ScrollbackLine` to hold `IReadOnlyList<object>` or a union type — high churn.
- Separate `ISegment` interface with polymorphic dispatch: significantly larger change, no proportionate benefit for two optional fields.
- Adding `LinkInfo` record as a nullable property: marginally cleaner as a value group, but adds a type with no other use; two flat nullable strings is simpler.

**Bottom line:** Adding `Command` and `Hint` as optional init-properties is the minimum-churn, zero-breaking-change approach.

### 5.2 MxpParserState

A small mutable class held per-`Session`, tracking the current negotiated markup state. Passed to `AnsiParser.Parse` on each line.

```csharp
// In SharpClient.Core/Rendering/MxpParserState.cs (new file)
public sealed class MxpParserState
{
    /// <summary>Whether MXP telnet option 91 was successfully negotiated.</summary>
    public bool IsMxpActive { get; set; }

    /// <summary>Whether Pueblo banner was detected and PUEBLOCLIENT handshake sent.</summary>
    public bool IsPuebloActive { get; set; }

    /// <summary>
    /// Current MXP line mode. Per-line modes (0,1,2,3) revert to DefaultLineMode at newline.
    /// Lock modes (5=LockOpen, 6=LockSecure) persist and set the default.
    /// </summary>
    public int LineMode { get; set; } = 0;

    /// <summary>Persistent default mode — 0 (Open) or 6 (LockSecure).</summary>
    public int DefaultLineMode { get; set; } = 0;

    /// <summary>
    /// Call at the start of each line to reset transient line mode to DefaultLineMode.
    /// </summary>
    public void BeginLine() => LineMode = DefaultLineMode;

    /// <summary>Apply an ESC[#z mode sequence.</summary>
    public void ApplyMode(int mode)
    {
        switch (mode)
        {
            case 0: case 1: case 2: case 3:
                LineMode = mode;
                break;
            case 5:
                LineMode = 0;
                DefaultLineMode = 0;
                break;
            case 6:
                LineMode = 1;
                DefaultLineMode = 1;
                break;
        }
    }

    /// <summary>True if the current line is in Secure mode (links are allowed).</summary>
    public bool IsSecure => LineMode == 1;
}
```

### 5.3 Parser Architecture

**Single-pass extension to `AnsiParser`** rather than a separate pre- or post-pass. Rationale: MXP line-mode sequences (`ESC[1z`) and MXP tags (`<SEND>`) and ANSI SGR codes (`ESC[31m`) can all appear on the same line interleaved; a single parser avoids multiple string scans and handles interleaving correctly.

**Signature change (additive, backward-compatible):**

```csharp
public static IReadOnlyList<StyledSegment> Parse(string line, MxpParserState? mxp = null)
```

All existing callers continue to work — `mxp` defaults to null.

**New parsing logic inside the main loop:**

1. **ESC[#z detection:** The existing CSI branch already reads any `ESC[...{final}` sequence. Extend the `if (final == 'm')` block to also handle `final == 'z'`:

```csharp
if (final == 'm')
{
    Flush();
    style = ApplySgr(style, parameters);
}
else if (final == 'z' && mxp is not null && mxp.IsMxpActive)
{
    if (int.TryParse(parameters, out var mode))
        mxp.ApplyMode(mode);
    // sequence consumed, not appended to text
}
```

2. **MXP/Pueblo tag detection:** After handling ESC sequences, when `current == '<'` and either `mxp.IsMxpActive` or `mxp.IsPuebloActive`, attempt tag parsing:

```csharp
if (current == '<' && mxp is not null && (mxp.IsMxpActive || mxp.IsPuebloActive))
{
    if (TryParseMxpTag(line, ref index, mxp, ref pendingCommand, ref pendingHint, out var skipToClose))
        continue;
}
```

**`TryParseMxpTag` logic (private static helper):**

- `<SEND href="cmd" hint="tip">`: extract `href` (the command), `hint`. Set `pendingCommand = cmd; pendingHint = tip`. Consume opening tag. Return `true` (skip the `<` char). Subsequent text accumulation goes into a segment that will get `Command = pendingCommand` when `</SEND>` is encountered.
- `</SEND>`: flush current text buffer as `StyledSegment { ..., Command = pendingCommand, Hint = pendingHint }`. Clear pending. Return `true`.
- `<SEND>cmd</SEND>` shorthand: treat the inner text as both display text and command. Simplest approach: on `</SEND>` when no href was set, use the accumulated text as both `Text` and `Command`.
- `<a xch_cmd="cmd" xch_hint="tip">` (Pueblo): same logic; fire only when `mxp.IsPuebloActive`.
- `</a>` (Pueblo): flush as link segment.
- Unknown tags / tags on non-Secure MXP lines: strip the tag text (consume until `>`), do not create link segments.
- MXP `<SEND>` on Open line (mode 0) when MXP active: **must strip tag and NOT set pendingCommand**. Security enforcement.

**State threaded through the loop:**

```csharp
string? pendingCommand = null;
string? pendingHint = null;

void Flush()
{
    if (text.Length == 0) return;
    segments.Add(new StyledSegment(text.ToString(), style)
    {
        Command = pendingCommand,
        Hint = pendingHint
    });
    text.Clear();
    pendingCommand = null;
    pendingHint = null;
}
```

Note: `Flush()` clears `pendingCommand` after use. The command is set immediately before the flush triggered by `</SEND>` — the helper sets it, then calls `Flush()`.

**Tag parser detail — `TryParseMxpTag`:**

Simple string scan from `index` (pointing at `<`):
1. Read until `>` to get the raw tag content.
2. Trim, split on whitespace to get tag name + attribute list.
3. Tag name comparison: `StringComparison.OrdinalIgnoreCase`.
4. Attribute parsing: scan for `name=value` or `name="value with spaces"` patterns.
5. Return `true` if consumed, `false` if the `<` should be treated as literal text (e.g., malformed or no closing `>`).

This does not need to be a full HTML parser. Only the specific tags and attributes we care about (SEND, /SEND, A with xch_cmd, /A) need handling; everything else is passed through or stripped.

### 5.4 Negotiation Wiring

**Step 1 — `ITelnetConnection`:** Add event:

```csharp
// In ITelnetConnection.cs
event Action? MxpEnabled;
```

**Step 2 — `TelnetConnection`:**

```csharp
public event Action? MxpEnabled;

// In ConnectAsync, change AddDefaultMUDProtocols call:
.AddDefaultMUDProtocols(
    onGMCPMessage: OnGmcpMessageAsync,
    onMSSP: OnMsspAsync,
    onMXPEnabled: OnMxpEnabledAsync)

// New handler:
private ValueTask OnMxpEnabledAsync()
{
    MxpEnabled?.Invoke();
    return ValueTask.CompletedTask;
}
```

**Step 3 — `Session`:** Hold `MxpParserState`, subscribe to events, detect Pueblo banner:

```csharp
private readonly MxpParserState _mxp = new();

// Constructor:
_connection.MxpEnabled += OnMxpEnabled;

// Handler:
private void OnMxpEnabled() => _mxp.IsMxpActive = true;

// In OnLineReceived:
private void OnLineReceived(string raw)
{
    // Pueblo banner detection (before stripping markup)
    if (!_mxp.IsPuebloActive && raw.Contains("This world is Pueblo", StringComparison.OrdinalIgnoreCase))
    {
        _mxp.IsPuebloActive = true;
        _ = _connection.SendAsync("PUEBLOCLIENT 2.01");
    }

    _mxp.BeginLine(); // reset transient mode to persistent default
    var line = new ScrollbackLine(AnsiParser.Parse(raw, _mxp));
    _scrollback.Add(line);
    LineAppended?.Invoke(line);
}

// Dispose:
_connection.MxpEnabled -= OnMxpEnabled;
```

### 5.5 Rendering in OutputView.razor

**Required new parameter:** `OutputView` needs to call back into the session to send a command. Currently it only receives `Lines`. The cleanest low-impact addition:

```razor
@code {
    [Parameter]
    public IReadOnlyList<ScrollbackLine> Lines { get; set; } = [];

    [Parameter]
    public Func<string, Task>? OnSendCommand { get; set; }
}
```

**Render loop change:**

```razor
@foreach (var segment in line.Segments)
{
    @if (segment.Command is not null && OnSendCommand is not null)
    {
        <button class="sc-link"
                style="@SegmentStyle.ToCss(segment)"
                title="@segment.Hint"
                @onclick="() => OnSendCommand(segment.Command!)">
            @(segment.Text.Length == 0 ? " " : segment.Text)
        </button>
    }
    else
    {
        <span style="@SegmentStyle.ToCss(segment)">@(segment.Text.Length == 0 ? " " : segment.Text)</span>
    }
}
```

**Wiring in SessionScreen.razor:**

```razor
<OutputView Lines="Vm.Active.Scrollback"
            OnSendCommand="cmd => Vm.Active!.SendAsync(cmd)" />
```

`Vm.Active` is non-null inside the `else` branch where `OutputView` is rendered.

**CSS for `.sc-link`:** Render as inline-block button that looks like a link — no border, no background, padding 0, cursor pointer, color accent (e.g., `#7ec8e3` or whatever the theme accent is), text-decoration underline. This avoids navigating away and makes the clickable intent clear.

```css
.sc-link {
    background: none;
    border: none;
    padding: 0;
    margin: 0;
    cursor: pointer;
    font: inherit;
    text-decoration: underline;
    color: #7ec8e3; /* phosphor-accent; tune to theme */
    display: inline;
}
.sc-link:hover {
    color: #aaddff;
}
```

### 5.6 Security Model

MXP line modes enforce server-side trust boundaries. The client's obligation:

| Line Mode | SEND/A tags | Behaviour |
|-----------|-------------|-----------|
| 0 — Open (default) | Secure elements forbidden | Strip tag, render text verbatim or suppress |
| 1 — Secure | Allowed | Parse, create clickable segment |
| 2 — Locked | No parsing at all | Pass `<` through as literal text |
| 5 — Lock Open | Persistent Open | Same as 0 |
| 6 — Lock Secure | Persistent Secure | Same as 1 |

**Enforcement in parser:** `TryParseMxpTag` checks `mxp.IsSecure` before creating a link. If not secure AND MXP is active, the tag must be consumed (stripped) but must NOT set `pendingCommand`. The display text inside `<SEND>...</SEND>` may optionally still be shown as plain text (preserving readability), or the whole tag including content can be stripped — server's choice of presentation.

**Pueblo:** No line-mode model. Any `<a xch_cmd>` tag in a Pueblo-active session is accepted. The server proved control by sending the banner; further content is equally trusted.

**Injection risk:** A player on a shared MUD cannot inject clickable links into another player's session unless they can inject the MXP control sequences and the server routes them through a Secure mode line. That is a server-side access-control problem, not a client-side one. The MXP spec acknowledges this and restricts SEND/A to Secure lines specifically to limit it. Our client honours that restriction.

---

## 6. Phased Task Breakdown

### Phase 1 — Segment Model & Parser (Core only, no UI)

| Task | File | Change |
|------|------|--------|
| 1.1 | `Core/Rendering/StyledSegment.cs` | Add `string? Command { get; init; }` and `string? Hint { get; init; }` non-positional properties |
| 1.2 | `Core/Rendering/MxpParserState.cs` (new) | `MxpParserState` class as designed in §5.2 |
| 1.3 | `Core/Rendering/AnsiParser.cs` | Add optional `MxpParserState? mxp` param; handle `ESC[#z`; add `TryParseMxpTag` private helper; wire `pendingCommand`/`pendingHint` into `Flush()` |
| 1.4 | `Tests/Rendering/AnsiParserTests.cs` | New tests: SEND on secure line produces segment with Command; SEND on open line produces no Command; ESC[1z activates secure; ESC[6z persists; Pueblo xch_cmd produces Command; malformed tag is stripped |

All existing tests must continue to pass without change.

### Phase 2 — Negotiation Wiring

| Task | File | Change |
|------|------|--------|
| 2.1 | `Core/Connection/ITelnetConnection.cs` | Add `event Action? MxpEnabled` |
| 2.2 | `Core/Connection/TelnetConnection.cs` | Implement event; wire `onMXPEnabled` in `AddDefaultMUDProtocols` call; add `OnMxpEnabledAsync` handler |
| 2.3 | `Core/Sessions/Session.cs` | Add `MxpParserState _mxp`; subscribe to `MxpEnabled`; Pueblo banner detection in `OnLineReceived`; pass `_mxp` to `AnsiParser.Parse`; `BeginLine()` at top of `OnLineReceived` |
| 2.4 | `Tests/Sessions/FakeTelnetConnection.cs` | Add stub `MxpEnabled` event to fake |

### Phase 3 — Rendering

| Task | File | Change |
|------|------|--------|
| 3.1 | `UI/Components/OutputView.razor` | Add `Func<string, Task>? OnSendCommand` parameter; conditional button/span rendering |
| 3.2 | `UI/Components/SessionScreen.razor` | Pass `OnSendCommand="cmd => Vm.Active!.SendAsync(cmd)"` to OutputView |
| 3.3 | CSS (wherever global styles live) | Add `.sc-link` button styles |

### Phase 4 — Integration Tests & Cleanup

| Task | File | Change |
|------|------|--------|
| 4.1 | `Tests/Sessions/SessionTests.cs` | Session integration: fake connection fires MxpEnabled → subsequent line with ESC[1z + SEND tag → scrollback segment has `Command != null` |
| 4.2 | Security test | Session integration: SEND tag on open line (no ESC[1z) → scrollback segment has `Command == null` |
| 4.3 | Pueblo test | Session integration: line containing "This world is Pueblo" triggers `IsPuebloActive`; subsequent `<a xch_cmd="north">` → segment with `Command = "north"` |
| 4.4 | UI.Tests | `OutputView` renders button for link segment; click fires `OnSendCommand` |

---

## 7. Feasibility & Effort Verdict

| Protocol | TNC support | App-layer work | Risk |
|----------|-------------|----------------|------|
| MXP (telnet option 91) | Full negotiation built-in, already included in `AddDefaultMUDProtocols` | Wire the callback (2 lines), parse markup in AnsiParser | Low — negotiation is done, markup parsing is straightforward string scanning |
| MXP markup parsing | None — app layer only | New `TryParseMxpTag` ~100 LoC, extend AnsiParser | Medium — tag parser must be robust to malformed/partial tags |
| MXP line-mode security | None — app layer only | `MxpParserState` + enforce in parser | Low effort, critical to get right |
| Pueblo | None | Banner detection in Session, xch_cmd tag parsing | Low — simpler than MXP (no line modes, only one tag) |

**Overall effort estimate:** ~300–400 LoC new/changed across ~10 files. Phases 1–3 can be done in one focused session. Phase 4 (tests) is a separate session.

**No blockers.** TNC's plugin model does not need extension. The entire markup stack is application-side. The segment model change is non-breaking. The UI change is a one-parameter addition.

---

## 8. Files to Create / Modify (Summary for Implementer)

```
NEW   src/SharpClient.Core/Rendering/MxpParserState.cs
MOD   src/SharpClient.Core/Rendering/StyledSegment.cs
MOD   src/SharpClient.Core/Rendering/AnsiParser.cs
MOD   src/SharpClient.Core/Connection/ITelnetConnection.cs
MOD   src/SharpClient.Core/Connection/TelnetConnection.cs
MOD   src/SharpClient.Core/Sessions/Session.cs
MOD   src/SharpClient.UI/Components/OutputView.razor
MOD   src/SharpClient.UI/Components/SessionScreen.razor
MOD   src/SharpClient.UI/[stylesheet]              (add .sc-link)
MOD   tests/SharpClient.Tests/Sessions/FakeTelnetConnection.cs
MOD   tests/SharpClient.Tests/Rendering/AnsiParserTests.cs
ADD   tests/SharpClient.Tests/Sessions/SessionMxpTests.cs (or extend existing)
ADD   tests/SharpClient.UI.Tests/OutputViewLinkTests.cs
```

`TelnetConnection.cs` does NOT need `AddPlugin<MXPProtocol>()` — it's already in `AddDefaultMUDProtocols`. Only the callback wire-up is missing.
