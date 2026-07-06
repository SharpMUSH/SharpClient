# SharpClient — Privacy Policy

**Effective date:** 2026-07-06
**Applies to:** the SharpClient Android application ("the app") published by the
SharpMUSH project ("we", "us").

## Summary

SharpClient is a client for connecting to text-based multiplayer games (MUD/MUSH
servers). **We do not collect, transmit, or receive any personal data.** The app
has no analytics, no advertising, no tracking, no user accounts with us, and no
third-party SDKs. All data the app stores stays **on your device**.

## What the app stores on your device

All of the following is stored only in the app's private storage on your device
and is never sent to us:

| Data | Where it is stored | Purpose |
| --- | --- | --- |
| Game server addresses (name, host, port) | Local SQLite database (`sharpclient.db`) | So you can reconnect to games you've saved |
| Character names | Local SQLite database | To identify your login on each game |
| Character passwords / connect strings | **OS-encrypted secure storage** (Android Keystore–backed) | To auto-log-in when you choose to save them |
| Triggers and aliases you create | Local SQLite database | Your automation rules |
| Session history / game transcripts | Local SQLite database (full-text index) | So you can scroll back and search what happened |
| Diagnostic and crash logs | Local file in the app's private folder (`logs/sharpclient.log`) | So you can export logs to report a bug |

The database and logs live in the app's private, sandboxed storage. Automatic
cloud backup is **disabled** (`allowBackup=false`), so this data is not copied to
Google Drive or any other backup service.

## Data sent over the network

When you connect to a game, the app opens a direct network connection **to the
server address you entered** and sends what you type — including your character
name and password at login — to **that server**. It does not send anything to us
or to any other party.

> **Important — connections are unencrypted.** Like virtually all MUD/MUSH
> servers, connections use the Telnet protocol, which is **not encrypted**. Your
> username, password, and everything you send and receive travel in plain text
> between your device and the game server. Only connect to servers you trust, and
> avoid reusing important passwords for game characters.

We are not the operator of the game servers you connect to. Those servers are
run by independent third parties and have their own privacy practices, which we
do not control.

## Data sharing

We do not share your data with anyone, because we never receive it in the first
place.

## Children's privacy

The app does not knowingly collect any personal information from anyone,
including children.

## Deleting your data

- Delete individual saved games, characters, or history from within the app.
- Uninstalling the app removes all of its local data, including the database,
  saved passwords, and logs.

## Changes to this policy

If this policy changes, the updated version will be published at the same
location with a new effective date.

## Contact

Questions about this policy can be raised via the project's issue tracker:
<https://github.com/SharpMUSH/SharpClient/issues>.
