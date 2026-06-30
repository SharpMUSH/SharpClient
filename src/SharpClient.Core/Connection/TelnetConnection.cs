using System.Net.Sockets;
using System.Text;
using TelnetNegotiationCore.Builders;
using TelnetNegotiationCore.Interpreters;
using TelnetNegotiationCore.Models;

namespace SharpClient.Core.Connection;

public sealed class TelnetConnection : ITelnetConnection
{
    private readonly ITelnetInterpreterFactory _factory;
    private readonly ReconnectOptions _reconnect;

    private TcpClient? _client;
    private TelnetInterpreter? _interpreter;
    private Task? _readTask;
    private Task? _monitorTask;
    private CancellationTokenSource? _reconnectCts;

    private string? _host;
    private int _port;
    private bool _intentionalDisconnect;

    public TelnetConnection(ITelnetInterpreterFactory factory, ReconnectOptions? reconnectOptions = null)
    {
        _factory = factory;
        _reconnect = reconnectOptions ?? ReconnectOptions.Default;
    }

    public event Action<string>? LineReceived;
    public event Action<ConnectionState>? StateChanged;
    public event Action<GmcpMessage>? GmcpReceived;
    public event Action<NegotiationEvent>? NegotiationReceived;
    public event Action? MxpEnabled;

    public ConnectionState State { get; private set; } = ConnectionState.Disconnected;

    /// <summary>
    /// Telnet CHARSET (RFC 2066) preference order offered during negotiation, most
    /// preferred first: UTF-8, then Latin-1 (ISO-8859-1), then US-ASCII. The negotiated
    /// result drives <see cref="TelnetInterpreter.CurrentEncoding"/>, which is used for
    /// both sending and receiving text.
    /// </summary>
    public static Encoding[] CharsetPreference { get; } = [Encoding.UTF8, Encoding.Latin1, Encoding.ASCII];

    /// <summary>The encoding currently negotiated with the server, or null when not connected.</summary>
    public Encoding? CurrentEncoding => _interpreter?.CurrentEncoding;

    public async Task ConnectAsync(string host, int port, CancellationToken cancellationToken = default)
    {
        await DisconnectAsync();

        _intentionalDisconnect = false;
        _reconnectCts = new CancellationTokenSource();

        SetState(ConnectionState.Connecting);
        try
        {
            await EstablishConnectionAsync(host, port, cancellationToken);
        }
        catch
        {
            _client?.Dispose();
            _client = null;
            _interpreter = null;
            SetState(ConnectionState.Error);
            throw;
        }
    }

    public async Task SendAsync(string line)
    {
        if (_interpreter is null)
        {
            throw new InvalidOperationException("Not connected.");
        }

        // TelnetNegotiationCore's SendAsync already appends the CR LF terminator
        // (and escapes IAC bytes). Appending our own would send a blank line after
        // every command. Also strip any stray CR/LF from the command text so a
        // single Send transmits exactly one clean line.
        var clean = line.Replace("\r", string.Empty).Replace("\n", string.Empty);
        var bytes = _interpreter.CurrentEncoding.GetBytes(clean);
        await _interpreter.SendAsync(bytes);
    }

    public async Task SendNawsAsync(int width, int height)
    {
        if (_interpreter is null)
        {
            return;
        }

        await _interpreter.SendNAWS((short)width, (short)height);
    }

    public async Task DisconnectAsync()
    {
        // Mark intentional first so the read-loop monitor does not treat the
        // resulting socket teardown as a drop and try to reconnect.
        _intentionalDisconnect = true;
        _reconnectCts?.Cancel();

        if (_interpreter is not null)
        {
            await _interpreter.DisposeAsync();
            _interpreter = null;
        }

        _client?.Dispose();
        _client = null;

        if (_readTask is not null)
        {
            await AwaitQuietlyAsync(_readTask);
            _readTask = null;
        }

        // Wait for the monitor (and any in-flight reconnect loop) to unwind before
        // disposing the CTS it observes.
        if (_monitorTask is not null)
        {
            await AwaitQuietlyAsync(_monitorTask);
            _monitorTask = null;
        }

        _reconnectCts?.Dispose();
        _reconnectCts = null;

        SetState(ConnectionState.Disconnected);
    }

    public async ValueTask DisposeAsync() => await DisconnectAsync();

    // Performs the actual TCP connect + telnet negotiation and starts the read-loop
    // monitor. Used both for the initial connect and for each reconnect attempt.
    private async Task EstablishConnectionAsync(string host, int port, CancellationToken cancellationToken)
    {
        var client = new TcpClient();
        // SO_KEEPALIVE lets the OS probe a silently-dead peer so a half-open socket
        // eventually surfaces as a read-loop completion instead of hanging forever.
        client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
        await client.ConnectAsync(host, port, cancellationToken);

        var (interpreter, readTask) = await _factory.CreateBuilder()
            .OnSubmit(OnSubmitAsync)
            .AddDefaultMUDProtocols(
                onGMCPMessage: OnGmcpMessageAsync,
                onMSSP: OnMsspAsync,
                onMXPEnabled: OnMxpEnabledAsync,
                charsetOrder: CharsetPreference)
            .BuildAndStartAsync(client, cancellationToken);

        _client = client;
        _interpreter = interpreter;
        _readTask = readTask;
        _host = host;
        _port = port;
        SetState(ConnectionState.Connected);
        _monitorTask = MonitorReadLoopAsync(readTask);
    }

    // Watches the interpreter's read loop. When it completes (server closed the socket
    // or a read error), and the teardown was not user-initiated, drives reconnection.
    private async Task MonitorReadLoopAsync(Task readTask)
    {
        await AwaitQuietlyAsync(readTask);

        if (_intentionalDisconnect || _reconnectCts is null)
        {
            return;
        }

        await ReconnectLoopAsync(_reconnectCts.Token);
    }

    private async Task ReconnectLoopAsync(CancellationToken token)
    {
        SetState(ConnectionState.Reconnecting);

        for (var attempt = 1; attempt <= _reconnect.MaxAttempts; attempt++)
        {
            try
            {
                await Task.Delay(_reconnect.DelayFor(attempt), token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (token.IsCancellationRequested || _intentionalDisconnect)
            {
                return;
            }

            // Tear down the dead client before the next attempt; keep the existing
            // state (Reconnecting) so we don't flicker through Disconnected.
            _client?.Dispose();
            _client = null;
            _interpreter = null;

            try
            {
                await EstablishConnectionAsync(_host!, _port, token);
                return;
            }
            catch
            {
                // Attempt failed; fall through to the next backoff iteration.
            }
        }

        if (!_intentionalDisconnect && !token.IsCancellationRequested)
        {
            SetState(ConnectionState.Error);
        }
    }

    private static async Task AwaitQuietlyAsync(Task task)
    {
        try
        {
            await task;
        }
        catch (OperationCanceledException) { }
        catch (System.IO.IOException) { }
        catch (ObjectDisposedException) { }
        catch (SocketException) { }
    }

    private ValueTask OnSubmitAsync(byte[] data, Encoding encoding, TelnetInterpreter interpreter)
    {
        LineReceived?.Invoke(encoding.GetString(data));
        return ValueTask.CompletedTask;
    }

    private ValueTask OnGmcpMessageAsync((string Package, string Info) msg)
    {
        GmcpReceived?.Invoke(new GmcpMessage(msg.Package, msg.Info));
        return ValueTask.CompletedTask;
    }

    private ValueTask OnMxpEnabledAsync()
    {
        MxpEnabled?.Invoke();
        NegotiationReceived?.Invoke(new NegotiationEvent("MXP", "enabled"));
        return ValueTask.CompletedTask;
    }

    private ValueTask OnMsspAsync(MSSPConfig cfg)
    {
        var parts = new List<string>();
        if (cfg.Name is not null) parts.Add($"NAME={cfg.Name}");
        if (cfg.Players is not null) parts.Add($"PLAYERS={cfg.Players}");
        if (cfg.Uptime is not null) parts.Add($"UPTIME={cfg.Uptime}");
        var detail = parts.Count > 0 ? string.Join(" ", parts) : "(no data)";
        NegotiationReceived?.Invoke(new NegotiationEvent("MSSP", detail));
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
