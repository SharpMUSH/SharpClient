# SharpClient — Design Tokens & Render Contract

Distilled from the approved visual design (`SharpClient.prototype.html`, an
interactive prototype; rendered states in `screenshots/`). This is the
implementation-facing reference for the MAUI Blazor Hybrid UI.

> **MAUI note:** the prototype is plain HTML/CSS. In our app the UI is Razor
> components in the **`SharpClient.UI`** Razor class library, hosted in a
> `BlazorWebView`. Tokens below become CSS custom properties in the RCL's
> `wwwroot` (e.g. `app.css`); the `runStyle` contract becomes the C# →
> inline-style mapping used by the output renderer. No browser-sandbox limits
> apply (native runtime), but the **UI is still authored as HTML/CSS/Razor**.

## Aesthetic

"Phosphor" modern-terminal: dark, CRT-tinged, monospace output on a themed dark
chrome. Optional **text glow** and **scanlines** (both user-toggleable). Accent
is swappable (default purple).

## Colour tokens (CSS variables)

| Token | Value | Use |
|-------|-------|-----|
| `--bg` | `#0b0e12` | app background |
| `--panel` | `#11151b` | panels, sheets, tab bar |
| `--elev` | `#171c24` | cards, raised controls |
| `--outbg` | `#090c10` | **output feed background** (ANSI renders on this) |
| `--tx` | `#c4ccd6` | primary UI text |
| `--dim` | `#7b8694` | secondary text |
| `--faint` | `#525b67` | tertiary / placeholder |
| `--pho` | `#c4d1c8` | **default phosphor text colour** for output |
| `--bd` | `rgba(255,255,255,.07)` | hairline border |
| `--bd2` | `rgba(255,255,255,.13)` | stronger border |
| `--acc` | `#9b7ed4` (default) | accent; alts: `#39d98a`, `#4a90d9`, `#c08a3e`, `#56b6c2` |
| `--acc2` | `lighten(acc, .22)` | brighter accent (buttons, active) |
| `--acc-soft` | `rgba(acc,.16)` | accent fill |
| `--acc-line` | `rgba(acc,.55)` | accent border |

Body backdrop: `radial-gradient(130% 90% at 50% -15%, #13171f 0%, #0a0c10 58%, #07090c 100%)`.

## Typography

- UI: **Space Grotesk** (`--ui`), system-ui fallback.
- Output: **JetBrains Mono** default (`--mono`); user-selectable among JetBrains
  Mono, IBM Plex Mono, Space Mono, Courier.
- Output auto-sizes to fit a **minimum column count** (user setting, 60–120,
  default 78), capped at a **max font size** (user setting, 10–18 px, default
  14). Formula from the prototype: `fontSize = innerWidthPx / (minCols * 0.6)`,
  clamped to `[6.5, fontCap]`.

## ANSI palette (16-colour, tuned for `--outbg`)

Index → hex. This **is** `AnsiColor.Indexed(0..15)`:

| idx | name | hex | idx | name | hex |
|-----|------|-----|-----|------|-----|
| 0 | black | `#3a3f4b` | 8 | bright black | `#5c6672` |
| 1 | red | `#e06c75` | 9 | bright red | `#ff8088` |
| 2 | green | `#8fc16f` | 10 | bright green | `#b5e890` |
| 3 | yellow | `#e5c07b` | 11 | bright yellow | `#ffd596` |
| 4 | blue | `#61afef` | 12 | bright blue | `#7cc4ff` |
| 5 | magenta | `#c678dd` | 13 | bright magenta | `#e29bf0` |
| 6 | cyan | `#56b6c2` | 14 | bright cyan | `#6fd3df` |
| 7 | white | `#abb2bf` | 15 | bright white | `#e8edf2` |

xterm-256: indices 16–231 are the 6×6×6 cube, 232–255 the grayscale ramp —
computed by the standard xterm formula (the prototype shows representative
samples). The renderer maps any `AnsiColor.Indexed(n)` for `n` in 0–255.

## Styled-segment render contract

Maps `StyledSegment` (`TextStyle` = fg/bg/bold/underline/inverse) to a rendered
span. Equivalent to the prototype's `runStyle`:

- Default foreground (no fg set) → `--pho` (`#c4d1c8`); default background →
  transparent (the `--outbg` shows through).
- `Foreground = Indexed(n)` → palette/cube hex; `Background = Indexed(n)` →
  hex + small horizontal padding (`0 2px`).
- **Inverse**: swap — foreground becomes `--outbg`, background becomes the
  segment's foreground colour (or `--pho` if none).
- **Bold** → font-weight 700. **Underline** → `text-decoration: underline`.
- Glow (when enabled): `text-shadow: 0 0 6px rgba(150,210,170,.28)` on the feed.

## Connection-state palette (drives tabs, pills, dots)

The design needs five states — **two beyond the current `ConnectionState`**
(`Reconnecting`, `Error`); see spec follow-ups.

| state | dot | pill text | pill fg | note |
|-------|-----|-----------|---------|------|
| live | `#8fc16f` | LIVE | `#b5e890` | dot glows |
| connecting | `#e5c07b` | CONNECTING | `#ffd596` | dot pulses |
| reconnecting | `#e5c07b` | RECONNECTING | `#ffd596` | dot pulses |
| disconnected | `#5c6672` | DISCONNECTED | `#9aa4b1` | |
| error | `#e06c75` | ERROR | `#ff8088` | |

## Screens & components (all in the prototype / screenshots)

- **World Manager** — Worlds list; each expands to its Characters; add/edit/
  delete World & Character; **Connect** opens a Session; empty state with
  quick-connect presets.
- **Session view** — tab bar (per-session state dot + close), output feed,
  input bar with command **history**, header with state pill + panel toggles.
- **Add World** / **Add Character** modals — Character has a **connect string**
  auto-sent on connect (stored as a secret).
- **Protocol Panel** — telnet negotiation rows (TTYPE/NAWS/MCCP2/CHARSET/ECHO/
  MSDP/MSSP) + collapsible GMCP JSON.
- **Triggers & Aliases** — rules (`regex` | `substr` | `alias`) → action
  (`highlight` | `send` | `notify`), per-rule on/off, scoped World ▸ Character.
- **Settings** — output font, minimum columns, max text size, column ruler.
- **Confirm** dialog; **phone** bottom nav (Worlds / Session) + **tablet** rail
  overlay. Phone frame 393×852, tablet 1140×724.

## Interaction states to implement

connecting · live · reconnecting · disconnected · error · empty (no worlds / no
characters / no sessions) · toast notifications.
