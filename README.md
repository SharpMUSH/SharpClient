# SharpClient

A .NET MAUI Blazor Hybrid client for connecting to text-based MUSH/MUD servers
over telnet, using [TelnetNegotiationCore](https://www.nuget.org/packages/TelnetNegotiationCore).
Android-primary. See [`docs/superpowers/specs`](docs/superpowers/specs) for the
design spec and visual-design brief.

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

Building `SharpClient.App` requires the `maui-android` workload (installed) **and**
the Android SDK + a JDK, which are not yet present on this machine. Install them
with:

```sh
dotnet build src/SharpClient.App/SharpClient.App.csproj \
  -t:InstallAndroidDependencies -p:AcceptAndroidSdkLicenses=True
```

MAUI has no Linux desktop head, so the day-to-day dev loop runs through the
`Core` + `Tests` projects; the App head builds/deploys to an Android device or
emulator.
