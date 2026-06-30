# SharpClient Core Pipeline Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the device-independent core of SharpClient — telnet connect/send/receive over TCP, ANSI/xterm-256 colour parsing into a styled segment model, and a minimal Session that ties them together — all unit-tested without an Android device.

**Architecture:** Pure C# in `SharpClient.Core`. `TelnetConnection` wraps TelnetNegotiationCore's `ITelnetInterpreterFactory` over a `TcpClient`, surfacing received lines as events. `AnsiParser` turns a raw line into a list of `StyledSegment`s. `Session` consumes a `TelnetConnection`, parses each line through `AnsiParser`, and appends to an observable scrollback buffer. Everything is exercised by TUnit, including `TelnetConnection` against an in-process loopback TCP server.

**Tech Stack:** .NET 10 (`net10.0`), TelnetNegotiationCore 2.5.0 (NuGet), TUnit 1.57, Microsoft.Extensions.DependencyInjection (test host for the TNC factory).

## Global Constraints

- Target framework: `net10.0` for `SharpClient.Core` and `SharpClient.Tests`.
- `TreatWarningsAsErrors=true` is active solution-wide (`Directory.Build.props`); every task must build at **0 warnings**.
- Nullable reference types and implicit usings are **enabled** globally — do not redeclare them in csproj files.
- File-scoped namespaces (enforced by `.editorconfig` as a warning → error).
- Test framework is **TUnit**, not xUnit: `[Test]`, `[Arguments(...)]`, and `await Assert.That(actual).IsEqualTo(expected)`.
- TelnetNegotiationCore is consumed from the **NuGet package** (already referenced in `SharpClient.Core.csproj`), never as a project reference.
- Run the suite with `dotnet run --project tests/SharpClient.Tests/SharpClient.Tests.csproj`. Filter a single test with `--treenode-filter` (TUnit/Microsoft.Testing.Platform), e.g. `... -- --treenode-filter "/*/*/AnsiParserTests/*"`.
- All paths below are relative to `/home/grave/RiderProjects/SharpClient`.

---

## File Structure

- `src/SharpClient.Core/Rendering/AnsiColor.cs` — colour value type (default or 0–255 indexed) + `AnsiColorKind`.
- `src/SharpClient.Core/Rendering/TextStyle.cs` — immutable style (fg/bg/bold/underline/inverse).
- `src/SharpClient.Core/Rendering/StyledSegment.cs` — a run of text + its `TextStyle`.
- `src/SharpClient.Core/Rendering/AnsiParser.cs` — parse a raw line into `IReadOnlyList<StyledSegment>`.
- `src/SharpClient.Core/Connection/ConnectionState.cs` — `Disconnected | Connecting | Connected`.
- `src/SharpClient.Core/Connection/TelnetConnection.cs` — TNC-backed connection; `ConnectAsync`/`SendAsync`/`DisconnectAsync`, `LineReceived`/`StateChanged` events.
- `src/SharpClient.Core/Sessions/Session.cs` — wraps a `TelnetConnection`, parses lines, appends to scrollback, raises `LineAppended`.
- `tests/SharpClient.Tests/Rendering/AnsiParserTests.cs`
- `tests/SharpClient.Tests/Connection/TelnetConnectionTests.cs`
- `tests/SharpClient.Tests/Connection/LoopbackServer.cs` — in-process TCP test double.
- `tests/SharpClient.Tests/Sessions/SessionTests.cs`

---

## Task 1: Colour and style value types

**Files:**
- Create: `src/SharpClient.Core/Rendering/AnsiColor.cs`
- Create: `src/SharpClient.Core/Rendering/TextStyle.cs`
- Create: `src/SharpClient.Core/Rendering/StyledSegment.cs`
- Test: `tests/SharpClient.Tests/Rendering/AnsiParserTests.cs` (created here, used from Task 2)

**Interfaces:**
- Produces:
  - `enum AnsiColorKind { Default, Indexed }`
  - `readonly record struct AnsiColor(AnsiColorKind Kind, int Index)` with `static AnsiColor Default` and `static AnsiColor Indexed(int index)`.
  - `readonly record struct TextStyle` with init properties `AnsiColor Foreground`, `AnsiColor Background`, `bool Bold`, `bool Underline`, `bool Inverse`, and `static TextStyle Default`.
  - `readonly record struct StyledSegment(string Text, TextStyle Style)`.

- [ ] **Step 1: Write the failing test**

Create `tests/SharpClient.Tests/Rendering/AnsiParserTests.cs`:

```csharp
using SharpClient.Core.Rendering;

namespace SharpClient.Tests.Rendering;

public sealed class AnsiParserTests
{
    [Test]
    public async Task DefaultStyleHasDefaultColours()
    {
        var style = TextStyle.Default;

        await Assert.That(style.Foreground).IsEqualTo(AnsiColor.Default);
        await Assert.That(style.Background).IsEqualTo(AnsiColor.Default);
        await Assert.That(style.Bold).IsFalse();
    }

    [Test]
    public async Task IndexedColourCarriesItsIndex()
    {
        var colour = AnsiColor.Indexed(200);

        await Assert.That(colour.Kind).IsEqualTo(AnsiColorKind.Indexed);
        await Assert.That(colour.Index).IsEqualTo(200);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet run --project tests/SharpClient.Tests/SharpClient.Tests.csproj`
Expected: FAIL — build error, `SharpClient.Core.Rendering` does not exist.

- [ ] **Step 3: Write minimal implementation**

Create `src/SharpClient.Core/Rendering/AnsiColor.cs`:

```csharp
namespace SharpClient.Core.Rendering;

public enum AnsiColorKind
{
    Default,
    Indexed,
}

public readonly record struct AnsiColor(AnsiColorKind Kind, int Index)
{
    public static AnsiColor Default => new(AnsiColorKind.Default, 0);

    public static AnsiColor Indexed(int index) => new(AnsiColorKind.Indexed, index);
}
```

Create `src/SharpClient.Core/Rendering/TextStyle.cs`:

```csharp
namespace SharpClient.Core.Rendering;

public readonly record struct TextStyle
{
    public AnsiColor Foreground { get; init; }

    public AnsiColor Background { get; init; }

    public bool Bold { get; init; }

    public bool Underline { get; init; }

    public bool Inverse { get; init; }

    public static TextStyle Default => new()
    {
        Foreground = AnsiColor.Default,
        Background = AnsiColor.Default,
    };
}
```

Create `src/SharpClient.Core/Rendering/StyledSegment.cs`:

```csharp
namespace SharpClient.Core.Rendering;

public readonly record struct StyledSegment(string Text, TextStyle Style);
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet run --project tests/SharpClient.Tests/SharpClient.Tests.csproj`
Expected: PASS — total 3 (the smoke test plus the two new ones).

- [ ] **Step 5: Commit**

```bash
git add src/SharpClient.Core/Rendering tests/SharpClient.Tests/Rendering
git commit -m "feat(core): add ANSI colour and styled-segment value types"
```

---

## Task 2: AnsiParser

**Files:**
- Create: `src/SharpClient.Core/Rendering/AnsiParser.cs`
- Test: `tests/SharpClient.Tests/Rendering/AnsiParserTests.cs` (extend)

**Interfaces:**
- Consumes: `AnsiColor`, `AnsiColorKind`, `TextStyle`, `StyledSegment` (Task 1).
- Produces: `static class AnsiParser` with `static IReadOnlyList<StyledSegment> Parse(string line)`.

Behaviour: split `line` on SGR escape sequences (`ESC [ ... m`, `ESC` = `\u001b`). Each non-empty text run becomes a `StyledSegment` carrying the style in effect. SGR parameters handled: `0` reset; `1` bold on; `4` underline on; `7` inverse on; `22` bold off; `24` underline off; `27` inverse off; `30–37` fg indexed `0–7`; `90–97` fg indexed `8–15`; `39` fg default; `40–47` bg indexed `0–7`; `100–107` bg indexed `8–15`; `49` bg default; `38;5;n` fg indexed `n`; `48;5;n` bg indexed `n`. An empty parameter list (`ESC[m`) means reset. Non-`m` CSI sequences (e.g. cursor moves ending in `A`–`H`, `J`, `K`) are consumed and discarded. A lone `ESC` with no valid CSI is dropped.

- [ ] **Step 1: Write the failing test** — append to `AnsiParserTests.cs`:

```csharp
    [Test]
    public async Task PlainTextIsOneDefaultSegment()
    {
        var segments = AnsiParser.Parse("hello world");

        await Assert.That(segments.Count).IsEqualTo(1);
        await Assert.That(segments[0].Text).IsEqualTo("hello world");
        await Assert.That(segments[0].Style).IsEqualTo(TextStyle.Default);
    }

    [Test]
    public async Task RedForegroundAppliesToFollowingText()
    {
        var segments = AnsiParser.Parse("\u001b[31mred\u001b[0m normal");

        await Assert.That(segments.Count).IsEqualTo(2);
        await Assert.That(segments[0].Text).IsEqualTo("red");
        await Assert.That(segments[0].Style.Foreground).IsEqualTo(AnsiColor.Indexed(1));
        await Assert.That(segments[1].Text).IsEqualTo(" normal");
        await Assert.That(segments[1].Style.Foreground).IsEqualTo(AnsiColor.Default);
    }

    [Test]
    public async Task BrightForegroundMapsToHighIndex()
    {
        var segments = AnsiParser.Parse("\u001b[92mbright");

        await Assert.That(segments[0].Style.Foreground).IsEqualTo(AnsiColor.Indexed(10));
    }

    [Test]
    public async Task Xterm256ForegroundIsParsed()
    {
        var segments = AnsiParser.Parse("\u001b[38;5;208morange");

        await Assert.That(segments[0].Style.Foreground).IsEqualTo(AnsiColor.Indexed(208));
    }

    [Test]
    public async Task BoldAndUnderlineCombine()
    {
        var segments = AnsiParser.Parse("\u001b[1;4mhi");

        await Assert.That(segments[0].Style.Bold).IsTrue();
        await Assert.That(segments[0].Style.Underline).IsTrue();
    }

    [Test]
    public async Task NonSgrCsiIsStripped()
    {
        var segments = AnsiParser.Parse("a\u001b[2Kb");

        await Assert.That(segments.Count).IsEqualTo(1);
        await Assert.That(segments[0].Text).IsEqualTo("ab");
    }

    [Test]
    public async Task EmptyLineProducesNoSegments()
    {
        var segments = AnsiParser.Parse(string.Empty);

        await Assert.That(segments.Count).IsEqualTo(0);
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet run --project tests/SharpClient.Tests/SharpClient.Tests.csproj`
Expected: FAIL — `AnsiParser` does not exist.

- [ ] **Step 3: Write minimal implementation**

Create `src/SharpClient.Core/Rendering/AnsiParser.cs`:

```csharp
using System.Text;

namespace SharpClient.Core.Rendering;

public static class AnsiParser
{
    private const char Escape = '\u001b';

    public static IReadOnlyList<StyledSegment> Parse(string line)
    {
        var segments = new List<StyledSegment>();
        var text = new StringBuilder();
        var style = TextStyle.Default;
        var index = 0;

        void Flush()
        {
            if (text.Length == 0)
            {
                return;
            }

            segments.Add(new StyledSegment(text.ToString(), style));
            text.Clear();
        }

        while (index < line.Length)
        {
            var current = line[index];
            if (current == Escape && index + 1 < line.Length && line[index + 1] == '[')
            {
                var end = index + 2;
                while (end < line.Length && !IsCsiFinal(line[end]))
                {
                    end++;
                }

                if (end < line.Length)
                {
                    var final = line[end];
                    var parameters = line.Substring(index + 2, end - (index + 2));
                    if (final == 'm')
                    {
                        Flush();
                        style = ApplySgr(style, parameters);
                    }

                    index = end + 1;
                    continue;
                }

                break;
            }

            if (current == Escape)
            {
                index++;
                continue;
            }

            text.Append(current);
            index++;
        }

        Flush();
        return segments;
    }

    private static bool IsCsiFinal(char c) => c is >= '@' and <= '~';

    private static TextStyle ApplySgr(TextStyle style, string parameters)
    {
        if (parameters.Length == 0)
        {
            return TextStyle.Default;
        }

        var codes = parameters.Split(';');
        for (var i = 0; i < codes.Length; i++)
        {
            if (!int.TryParse(codes[i], out var code))
            {
                continue;
            }

            switch (code)
            {
                case 0:
                    style = TextStyle.Default;
                    break;
                case 1:
                    style = style with { Bold = true };
                    break;
                case 4:
                    style = style with { Underline = true };
                    break;
                case 7:
                    style = style with { Inverse = true };
                    break;
                case 22:
                    style = style with { Bold = false };
                    break;
                case 24:
                    style = style with { Underline = false };
                    break;
                case 27:
                    style = style with { Inverse = false };
                    break;
                case 39:
                    style = style with { Foreground = AnsiColor.Default };
                    break;
                case 49:
                    style = style with { Background = AnsiColor.Default };
                    break;
                case 38 when TryReadExtended(codes, ref i, out var fg):
                    style = style with { Foreground = fg };
                    break;
                case 48 when TryReadExtended(codes, ref i, out var bg):
                    style = style with { Background = bg };
                    break;
                case >= 30 and <= 37:
                    style = style with { Foreground = AnsiColor.Indexed(code - 30) };
                    break;
                case >= 90 and <= 97:
                    style = style with { Foreground = AnsiColor.Indexed(code - 90 + 8) };
                    break;
                case >= 40 and <= 47:
                    style = style with { Background = AnsiColor.Indexed(code - 40) };
                    break;
                case >= 100 and <= 107:
                    style = style with { Background = AnsiColor.Indexed(code - 100 + 8) };
                    break;
            }
        }

        return style;
    }

    private static bool TryReadExtended(string[] codes, ref int i, out AnsiColor colour)
    {
        colour = AnsiColor.Default;
        if (i + 2 < codes.Length && codes[i + 1] == "5" && int.TryParse(codes[i + 2], out var n))
        {
            colour = AnsiColor.Indexed(n);
            i += 2;
            return true;
        }

        return false;
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet run --project tests/SharpClient.Tests/SharpClient.Tests.csproj`
Expected: PASS — all AnsiParser tests green.

- [ ] **Step 5: Commit**

```bash
git add src/SharpClient.Core/Rendering/AnsiParser.cs tests/SharpClient.Tests/Rendering/AnsiParserTests.cs
git commit -m "feat(core): parse ANSI/xterm-256 SGR sequences into styled segments"
```

---

## Task 3: TelnetConnection

**Files:**
- Create: `src/SharpClient.Core/Connection/ConnectionState.cs`
- Create: `src/SharpClient.Core/Connection/TelnetConnection.cs`
- Create: `tests/SharpClient.Tests/Connection/LoopbackServer.cs`
- Test: `tests/SharpClient.Tests/Connection/TelnetConnectionTests.cs`
- Modify: `tests/SharpClient.Tests/SharpClient.Tests.csproj` (add DI + logging packages)

**Interfaces:**
- Consumes: TelnetNegotiationCore `ITelnetInterpreterFactory` (namespace `TelnetNegotiationCore.Builders`), `TelnetInterpreter` (namespace `TelnetNegotiationCore.Interpreters`).
- Produces:
  - `enum ConnectionState { Disconnected, Connecting, Connected }`
  - `sealed class TelnetConnection(ITelnetInterpreterFactory factory) : IAsyncDisposable` with:
    - `event Action<string>? LineReceived;`
    - `event Action<ConnectionState>? StateChanged;`
    - `ConnectionState State { get; }`
    - `Task ConnectAsync(string host, int port, CancellationToken cancellationToken = default)`
    - `Task SendAsync(string line)`
    - `Task DisconnectAsync()`

Note on TNC 2.5.0 API: build with `factory.CreateBuilder().OnSubmit(callback).BuildAndStartAsync(tcpClient, ct)` which returns `(TelnetInterpreter Interpreter, Task ReadTask)`. The `OnSubmit` callback signature is `Func<byte[], System.Text.Encoding, TelnetInterpreter, ValueTask>`; its `byte[]` argument is one received line (no trailing newline). Send with `interpreter.SendAsync(byte[])`; decode/encode with `interpreter.CurrentEncoding`.

- [ ] **Step 1: Add test dependencies**

Run:
```bash
dotnet add tests/SharpClient.Tests/SharpClient.Tests.csproj package Microsoft.Extensions.DependencyInjection
dotnet add tests/SharpClient.Tests/SharpClient.Tests.csproj package Microsoft.Extensions.Logging
```
Expected: both restore successfully.

- [ ] **Step 2: Write the loopback test server**

Create `tests/SharpClient.Tests/Connection/LoopbackServer.cs`:

```csharp
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace SharpClient.Tests.Connection;

/// <summary>A minimal raw-TCP server: accepts one client, lets the test push
/// bytes to it and read what the client sends back.</summary>
public sealed class LoopbackServer : IAsyncDisposable
{
    private readonly TcpListener _listener;
    private TcpClient? _client;
    private NetworkStream? _stream;

    public LoopbackServer()
    {
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
    }

    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

    public async Task AcceptAsync()
    {
        _client = await _listener.AcceptTcpClientAsync();
        _stream = _client.GetStream();
    }

    public async Task SendLineAsync(string line)
    {
        var bytes = Encoding.ASCII.GetBytes(line + "\r\n");
        await _stream!.WriteAsync(bytes);
        await _stream.FlushAsync();
    }

    public async Task<string> ReadAvailableAsync(TimeSpan timeout)
    {
        var buffer = new byte[1024];
        using var cts = new CancellationTokenSource(timeout);
        var read = await _stream!.ReadAsync(buffer, cts.Token);
        return Encoding.ASCII.GetString(buffer, 0, read);
    }

    public async ValueTask DisposeAsync()
    {
        _stream?.Dispose();
        _client?.Dispose();
        _listener.Stop();
        await ValueTask.CompletedTask;
    }
}
```

- [ ] **Step 3: Write the failing test**

Create `tests/SharpClient.Tests/Connection/TelnetConnectionTests.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using SharpClient.Core.Connection;
using TelnetNegotiationCore.Builders;

namespace SharpClient.Tests.Connection;

public sealed class TelnetConnectionTests
{
    private static ITelnetInterpreterFactory CreateFactory()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTelnetClient();
        return services.BuildServiceProvider().GetRequiredService<ITelnetInterpreterFactory>();
    }

    [Test]
    public async Task ReceivesLineFromServer()
    {
        await using var server = new LoopbackServer();
        await using var connection = new TelnetConnection(CreateFactory());

        var received = new TaskCompletionSource<string>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        connection.LineReceived += line => received.TrySetResult(line);

        var accept = server.AcceptAsync();
        await connection.ConnectAsync("127.0.0.1", server.Port);
        await accept;

        await server.SendLineAsync("Hello, MUSH");

        var line = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(line).IsEqualTo("Hello, MUSH");
    }

    [Test]
    public async Task SendDeliversBytesToServer()
    {
        await using var server = new LoopbackServer();
        await using var connection = new TelnetConnection(CreateFactory());

        var accept = server.AcceptAsync();
        await connection.ConnectAsync("127.0.0.1", server.Port);
        await accept;

        await connection.SendAsync("look");

        var sent = await server.ReadAvailableAsync(TimeSpan.FromSeconds(5));
        await Assert.That(sent.Contains("look")).IsTrue();
    }

    [Test]
    public async Task StateBecomesConnectedAfterConnect()
    {
        await using var server = new LoopbackServer();
        await using var connection = new TelnetConnection(CreateFactory());

        var accept = server.AcceptAsync();
        await connection.ConnectAsync("127.0.0.1", server.Port);
        await accept;

        await Assert.That(connection.State).IsEqualTo(ConnectionState.Connected);
    }
}
```

- [ ] **Step 4: Run test to verify it fails**

Run: `dotnet run --project tests/SharpClient.Tests/SharpClient.Tests.csproj`
Expected: FAIL — `TelnetConnection` / `ConnectionState` do not exist.

- [ ] **Step 5: Write minimal implementation**

Create `src/SharpClient.Core/Connection/ConnectionState.cs`:

```csharp
namespace SharpClient.Core.Connection;

public enum ConnectionState
{
    Disconnected,
    Connecting,
    Connected,
}
```

Create `src/SharpClient.Core/Connection/TelnetConnection.cs`:

```csharp
using System.Net.Sockets;
using System.Text;
using TelnetNegotiationCore.Builders;
using TelnetNegotiationCore.Interpreters;

namespace SharpClient.Core.Connection;

public sealed class TelnetConnection(ITelnetInterpreterFactory factory) : IAsyncDisposable
{
    private TcpClient? _client;
    private TelnetInterpreter? _interpreter;
    private Task? _readTask;

    public event Action<string>? LineReceived;

    public event Action<ConnectionState>? StateChanged;

    public ConnectionState State { get; private set; } = ConnectionState.Disconnected;

    public async Task ConnectAsync(string host, int port, CancellationToken cancellationToken = default)
    {
        SetState(ConnectionState.Connecting);
        try
        {
            _client = new TcpClient();
            await _client.ConnectAsync(host, port, cancellationToken);

            var (interpreter, readTask) = await factory.CreateBuilder()
                .OnSubmit(OnSubmitAsync)
                .BuildAndStartAsync(_client, cancellationToken);

            _interpreter = interpreter;
            _readTask = readTask;
            SetState(ConnectionState.Connected);
        }
        catch
        {
            SetState(ConnectionState.Disconnected);
            throw;
        }
    }

    public async Task SendAsync(string line)
    {
        if (_interpreter is null)
        {
            throw new InvalidOperationException("Not connected.");
        }

        var bytes = _interpreter.CurrentEncoding.GetBytes(line + "\r\n");
        await _interpreter.SendAsync(bytes);
    }

    public async Task DisconnectAsync()
    {
        if (_interpreter is not null)
        {
            await _interpreter.DisposeAsync();
            _interpreter = null;
        }

        _client?.Dispose();
        _client = null;
        SetState(ConnectionState.Disconnected);
    }

    public async ValueTask DisposeAsync() => await DisconnectAsync();

    private ValueTask OnSubmitAsync(byte[] data, Encoding encoding, TelnetInterpreter interpreter)
    {
        LineReceived?.Invoke(encoding.GetString(data));
        return ValueTask.CompletedTask;
    }

    private void SetState(ConnectionState state)
    {
        if (State == state)
        {
            return;
        }

        State = state;
        StateChanged?.Invoke(state);
    }
}
```

- [ ] **Step 6: Run test to verify it passes**

Run: `dotnet run --project tests/SharpClient.Tests/SharpClient.Tests.csproj`
Expected: PASS. If `ReceivesLineFromServer` times out, the executor should verify TNC's `OnSubmit` line-termination (it fires on newline); the loopback server already sends `\r\n`. If `SendDeliversBytesToServer` shows extra negotiation bytes before `look`, the `Contains` assertion still holds.

- [ ] **Step 7: Commit**

```bash
git add src/SharpClient.Core/Connection tests/SharpClient.Tests/Connection tests/SharpClient.Tests/SharpClient.Tests.csproj
git commit -m "feat(core): add TelnetConnection over TelnetNegotiationCore with loopback tests"
```

---

## Task 4: Session

**Files:**
- Create: `src/SharpClient.Core/Sessions/Session.cs`
- Test: `tests/SharpClient.Tests/Sessions/SessionTests.cs`

**Interfaces:**
- Consumes: `TelnetConnection`, `ConnectionState` (Task 3); `AnsiParser`, `StyledSegment` (Tasks 1–2).
- Produces:
  - `sealed record ScrollbackLine(IReadOnlyList<StyledSegment> Segments)`
  - `sealed class Session(TelnetConnection connection) : IAsyncDisposable` with:
    - `IReadOnlyList<ScrollbackLine> Scrollback { get; }`
    - `event Action<ScrollbackLine>? LineAppended;`
    - `ConnectionState State { get; }`
    - `Task ConnectAsync(string host, int port, CancellationToken cancellationToken = default)`
    - `Task SendAsync(string line)`

The `Session` subscribes to the connection's `LineReceived`, runs each line through `AnsiParser.Parse`, wraps the result in a `ScrollbackLine`, appends it to an internal list, and raises `LineAppended`. It forwards `State` from the connection.

- [ ] **Step 1: Write the failing test**

Create `tests/SharpClient.Tests/Sessions/SessionTests.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using SharpClient.Core.Connection;
using SharpClient.Core.Sessions;
using SharpClient.Tests.Connection;
using TelnetNegotiationCore.Builders;

namespace SharpClient.Tests.Sessions;

public sealed class SessionTests
{
    private static ITelnetInterpreterFactory CreateFactory()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTelnetClient();
        return services.BuildServiceProvider().GetRequiredService<ITelnetInterpreterFactory>();
    }

    [Test]
    public async Task ReceivedLineIsParsedIntoScrollback()
    {
        await using var server = new LoopbackServer();
        await using var session = new Session(new TelnetConnection(CreateFactory()));

        var appended = new TaskCompletionSource<ScrollbackLine>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        session.LineAppended += line => appended.TrySetResult(line);

        var accept = server.AcceptAsync();
        await session.ConnectAsync("127.0.0.1", server.Port);
        await accept;

        await server.SendLineAsync("\u001b[31mAlert\u001b[0m");

        var line = await appended.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(line.Segments.Count).IsEqualTo(1);
        await Assert.That(line.Segments[0].Text).IsEqualTo("Alert");
        await Assert.That(session.Scrollback.Count).IsEqualTo(1);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet run --project tests/SharpClient.Tests/SharpClient.Tests.csproj`
Expected: FAIL — `Session` / `ScrollbackLine` do not exist.

- [ ] **Step 3: Write minimal implementation**

Create `src/SharpClient.Core/Sessions/Session.cs`:

```csharp
using SharpClient.Core.Connection;
using SharpClient.Core.Rendering;

namespace SharpClient.Core.Sessions;

public sealed record ScrollbackLine(IReadOnlyList<StyledSegment> Segments);

public sealed class Session : IAsyncDisposable
{
    private readonly TelnetConnection _connection;
    private readonly List<ScrollbackLine> _scrollback = [];

    public Session(TelnetConnection connection)
    {
        _connection = connection;
        _connection.LineReceived += OnLineReceived;
    }

    public IReadOnlyList<ScrollbackLine> Scrollback => _scrollback;

    public event Action<ScrollbackLine>? LineAppended;

    public ConnectionState State => _connection.State;

    public Task ConnectAsync(string host, int port, CancellationToken cancellationToken = default) =>
        _connection.ConnectAsync(host, port, cancellationToken);

    public Task SendAsync(string line) => _connection.SendAsync(line);

    public async ValueTask DisposeAsync()
    {
        _connection.LineReceived -= OnLineReceived;
        await _connection.DisposeAsync();
    }

    private void OnLineReceived(string raw)
    {
        var line = new ScrollbackLine(AnsiParser.Parse(raw));
        _scrollback.Add(line);
        LineAppended?.Invoke(line);
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet run --project tests/SharpClient.Tests/SharpClient.Tests.csproj`
Expected: PASS — all tests across the suite green.

- [ ] **Step 5: Commit**

```bash
git add src/SharpClient.Core/Sessions tests/SharpClient.Tests/Sessions
git commit -m "feat(core): add Session that parses received lines into scrollback"
```

---

## Done — Phase 1 deliverable

After Task 4 the core pipeline is complete and fully tested without a device:
connect to a MUSH over TCP, surface coloured output as parsed scrollback, and
send commands. Subsequent plans (UI shell + colour rendering, Worlds &
Characters persistence, GMCP/MSDP/NAWS + Protocol Panel, triggers/aliases +
logging) build on these types and are written once the visual-design details
land.
