# SharpClient

A .NET MAUI Blazor Hybrid client for connecting to text-based MUSH/MUD servers
over telnet, using [TelnetNegotiationCore](https://www.nuget.org/packages/TelnetNegotiationCore).
Android-primary. See [`docs/superpowers/specs`](docs/superpowers/specs) for the
design spec and visual-design brief.

## Installing on Android

Signed APKs are attached to every [GitHub Release](https://github.com/SharpMUSH/SharpClient/releases).
You can sideload the `.apk` directly, but the easiest way to install **and stay
updated** is [Obtainium](https://github.com/ImranR98/Obtainium), which tracks this
repo's releases for you:

1. Install Obtainium.
2. Add an app with this source URL:
   ```
   https://github.com/SharpMUSH/SharpClient
   ```
   or, from your phone, paste this Obtainium deep link (GitHub can't render it as
   a clickable link, so copy it as-is):
   ```
   obtainium://add/https://github.com/SharpMUSH/SharpClient
   ```
3. Obtainium picks up the universal signed APK from each release and notifies you
   when a new version ships.

Each release ships a single universal APK, so no ABI/architecture filter is
needed. The APK's `versionName`/`versionCode` are derived from the release tag
(e.g. `v0.2` → `0.2`), so Obtainium and Android both see the correct version and
can update in place. If a release is marked **pre-release** on GitHub, enable
"include prereleases" for the app in Obtainium or it will be skipped.

## Solution layout

| Project | Kind | Purpose |
|---------|------|---------|
| `src/SharpClient.Core` | class library (`net10.0`) | Pure, MAUI-free logic: telnet connection, ANSI parsing, triggers/aliases, world/character model, sessions. Testable on plain .NET. |
| `src/SharpClient.UI` | Razor class library (`net10.0`) | Shared Blazor UI components. |
| `src/SharpClient.App` | MAUI Blazor Hybrid app (`net10.0-android`) | The device app head. Hosts the UI, provides platform implementations, wires DI. |
| `tests/SharpClient.Tests` | TUnit (`net10.0`) | Unit tests for the `Core` logic. |

Shared build settings (warnings-as-errors, nullable, modern C#) live in
[`Directory.Build.props`](Directory.Build.props); the SDK is pinned to .NET 10 via
[`global.json`](global.json).

## Building & testing

```sh
# Core + UI + Tests (no Android SDK required)
dotnet build SharpClient.slnx          # note: excludes the App head if it can't build
dotnet run --project tests/SharpClient.Tests   # run the TUnit suite
```

### Android app head

```sh
dotnet build src/SharpClient.App/SharpClient.App.csproj -f net10.0-android
```

Building `SharpClient.App` requires the `maui-android` workload plus the Android
SDK and **JDK 17** (the .NET for Android toolchain rejects newer JDKs). On this
machine those live at `~/Android/Sdk` and `~/Android/jdk-17.0.19+10`, wired in via
a gitignored `Directory.Local.props` (see `Directory.Local.props` for the property
names). On a fresh machine, install them with:

```sh
# 1. Android SDK (accepts licenses, downloads platform-tools/build-tools/platform)
dotnet build src/SharpClient.App/SharpClient.App.csproj -f net10.0-android \
  -t:InstallAndroidDependencies -p:AcceptAndroidSdkLicenses=True \
  -p:AndroidSdkDirectory=$HOME/Android/Sdk -p:JavaSdkDirectory=<jdk-17-path>

# 2. Point Directory.Local.props at your AndroidSdkDirectory and JavaSdkDirectory
```

MAUI has no Linux desktop head, so the day-to-day dev loop runs through the
`Core` + `Tests` projects; the App head builds/deploys to an Android device or
emulator.
