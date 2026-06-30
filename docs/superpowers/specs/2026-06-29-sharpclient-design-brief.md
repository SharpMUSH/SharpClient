# SharpClient — Visual & Interaction Design Brief

*For frontend/visual design. Derived from the SharpClient design spec
(2026-06-29). Backend/protocol internals omitted; this is the UI surface only.*

## What it is

SharpClient is a **.NET MAUI Blazor Hybrid** mobile client for connecting to
text-based **MUSH/MUD** servers. **Android-primary** (phone + tablet); also runs on
desktop for development. The UI is **Razor/Blazor in a `BlazorWebView`** — so design
in **HTML/CSS** terms (CSS variables, flex/grid, mobile-first). The defining surface
is a live, scrolling feed of server text that arrives with arbitrary color.

## Domain model the UI must express

- **World** — a MUSH (name, host, port). Contains Characters.
- **Character** — belongs to a World (display name, optional saved login). The thing
  you actually connect as.
- **Session** — a live connection for one Character, shown as a **tab**. Several
  Sessions (across different Characters/Worlds) run at once.

So: *World ▸ Character ▸ (Connect) ▸ Session tab.*

## Screens / regions to design

1. **World Manager** — list of Worlds; each expands to its Characters. Actions: add/
   edit World, add/edit Character, **Connect** (Character → opens a Session tab).
   Needs a first-run **empty state** (no worlds yet).
2. **Session view** (the main screen):
   - **Tab bar** across multiple live sessions (with connection-state indicator per
     tab, and a close/disconnect affordance).
   - **Output view** — large, virtualized, scrollable feed rendering server text in
     **ANSI + xterm-256 color**: arbitrary foreground/background per run of text,
     plus bold / underline / inverse. Thousands of lines of scrollback.
   - **Input bar** — text field + send, command history recall, on-screen-keyboard
     aware (input stays visible above the keyboard).
3. **Protocol Panel** — a collapsible debug drawer showing live telnet negotiation
   data as labeled key/value/JSON (for development/debugging; secondary surface).
4. **Trigger / Alias editor** — declarative rules: a match pattern (regex/substring)
   → action (highlight / send / notify). List + add/edit form. Rules scope to a World
   or a Character.

## Constraints & priorities

- **ANSI coexistence (the hard one):** the server injects *arbitrary* colored text.
  The app's own chrome/theme must coexist with raw ANSI without fighting it — define
  how a themed (likely dark) background renders default / 8 / 16 / 256-color fg+bg
  text and keeps it legible. Pick a readable **monospace** stack for output.
- **Mobile-first:** generous touch targets, one-handed reach, phone *and* tablet
  layouts (tablet can show World Manager + Session side by side; phone navigates
  between them).
- **Performance feel:** virtualized scrollback; smooth at thousands of lines.
- **States to cover:** connecting / live / reconnecting / disconnected / error; empty
  states (no worlds, no characters, empty session); long-press/selection on output.

## Deliverables

- A couple of **distinct visual directions** before converging — it can lean
  modern-terminal / retro-MUD without becoming an unreadable novelty.
- A **color system** (app chrome) plus an explicit **ANSI palette** that sits well on
  the chosen background.
- **Typography** (UI + monospace output).
- **Tab UX** for multiple live sessions on a small screen.
- **Component states** (connecting / live / error / empty) for tabs, output, input,
  and the World/Character list.
