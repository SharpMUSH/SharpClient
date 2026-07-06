# Google Play — Data Safety declaration

This document records the answers to fill into the **Play Console → App content →
Data safety** form, with the reasoning behind each answer so it can be defended
or revisited. It reflects the app as of 2026-07-06. Update it if data handling
changes.

See also: [`privacy-policy.md`](./privacy-policy.md).

## Key facts the form is based on

- The app stores everything **locally on the device** (SQLite DB + OS secure
  storage + a local log file). Nothing is sent to the developer.
- The only network traffic is a **direct, user-initiated connection to the
  game (MUD/MUSH) server the user typed in.** That server is a third party the
  user chooses, not an endpoint we operate.
- No analytics, ads, crash-reporting SDKs, or other third-party SDKs are bundled.

## Form answers

### 1. Data collection and sharing

**Does your app collect or share any of the required user data types?** → **No.**

Rationale: In Play's model, "collected" means data transmitted off the device to
the developer or a service acting on the developer's behalf, and "shared" means
transferred to a third party. SharpClient does neither. Passwords, character
names, server addresses, transcripts, triggers, and logs are all kept in the
app's private on-device storage and are never transmitted to us.

The character name and password **are** sent to the game server at login, but:
- the server is an endpoint **the user explicitly enters and connects to**, and
- the transfer is core, user-initiated functionality (the same category as an
  SSH, FTP, email, or IRC client sending credentials to a user-specified host),
which Play's guidance does not treat as developer "collection" or "sharing."

> ⚠️ **Confirm before submitting:** this "No data collected/shared" position
> rests on the user-directed-server interpretation above. It is the standard
> position for this class of app, but you own the final declaration. If you
> prefer maximum caution, you can instead declare *App activity / "Other
> user-generated content"* and *Personal info / "User IDs"* as **collected but
> not shared**, processed for **App functionality**, and mark it not required —
> at the cost of a more alarming store label. Recommended answer remains **No.**

### 2. Security practices

- **Is data encrypted in transit?**
  Disclose honestly: connections to game servers use **Telnet, which is not
  encrypted**. If the form scopes this question only to *collected* data (of
  which there is none), it may be N/A — but the privacy policy states the
  cleartext fact plainly regardless.
- **Data stored on device is protected:** saved passwords use Android
  Keystore–backed secure storage; automatic cloud backup is disabled
  (`allowBackup=false`).

### 3. Data deletion

- **Can users request that their data be deleted?** → Users delete data
  themselves in-app, and uninstalling removes all local data. No server-side
  data exists to delete, so no deletion request mechanism is needed.

## Privacy policy URL (required by Play)

The Data Safety form and the store listing both require a public **Privacy
policy URL**. `privacy-policy.md` is the content; it needs to be reachable at a
stable public URL. Options, easiest first:

1. **GitHub Pages** — enable Pages for the repo and publish `privacy-policy.md`,
   giving e.g. `https://sharpmush.github.io/SharpClient/privacy-policy`.
2. **Raw GitHub URL** — Play accepts a link to the rendered file, e.g.
   `https://github.com/SharpMUSH/SharpClient/blob/master/docs/store/privacy-policy.md`.

Pick one and paste it into both **Data safety** and **Store listing → Privacy
policy**.
