using System.Net.Sockets;
using System.Text;
using TelnetNegotiationCore.Builders;
using TelnetNegotiationCore.Interpreters;
using TelnetNegotiationCore.Models;

namespace SharpClient.Core.Connection;

public sealed class TelnetConnection(ITelnetInterpreterFactory factory) : ITelnetConnection
{
    private TcpClient? _client;
    private TelnetInterpreter? _interpreter;
    private Task? _readTask;

    public event Action<string>? LineReceived;
    public event Action<ConnectionState>? StateChanged;
    public event Action<GmcpMessage>? GmcpReceived;
    public event Action<NegotiationEvent>? NegotiationReceived;

    public ConnectionState State { get; private set; } = ConnectionState.Disconnected;

    public async Task ConnectAsync(string host, int port, CancellationToken cancellationToken = default)
    {
        if (_client is not null)
        {
            await DisconnectAsync();
        }

        SetState(ConnectionState.Connecting);
        try
        {
            _client = new TcpClient();
            await _client.ConnectAsync(host, port, cancellationToken);

            var (interpreter, readTask) = await factory.CreateBuilder()
                .OnSubmit(OnSubmitAsync)
                .AddDefaultMUDProtocols(
                    onGMCPMessage: OnGmcpMessageAsync,
                    onMSSP: OnMsspAsync)
                .BuildAndStartAsync(_client, cancellationToken);

            _interpreter = interpreter;
            _readTask = readTask;
            SetState(ConnectionState.Connected);
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

        var bytes = _interpreter.CurrentEncoding.GetBytes(line + "\r\n");
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
        if (_interpreter is not null)
        {
            await _interpreter.DisposeAsync();
            _interpreter = null;
        }

        _client?.Dispose();
        _client = null;

        if (_readTask is not null)
        {
            try
            {
                await _readTask;
            }
            catch (OperationCanceledException) { }
            catch (System.IO.IOException) { }
            catch (ObjectDisposedException) { }
            _readTask = null;
        }

        SetState(ConnectionState.Disconnected);
    }

    public async ValueTask DisposeAsync() => await DisconnectAsync();

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
