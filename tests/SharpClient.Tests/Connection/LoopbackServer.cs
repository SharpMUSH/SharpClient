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

    /// <summary>Writes raw bytes to the client (e.g. a hand-built telnet negotiation sequence).</summary>
    public async Task SendBytesAsync(byte[] bytes)
    {
        await _stream!.WriteAsync(bytes);
        await _stream.FlushAsync();
    }

    /// <summary>Reads whatever bytes are currently available from the client, raw.</summary>
    public async Task<byte[]> ReadAvailableBytesAsync(TimeSpan timeout)
    {
        var buffer = new byte[4096];
        using var cts = new CancellationTokenSource(timeout);
        var read = await _stream!.ReadAsync(buffer, cts.Token);
        return buffer[..read];
    }

    public async ValueTask DisposeAsync()
    {
        _stream?.Dispose();
        _client?.Dispose();
        _listener.Stop();
        await ValueTask.CompletedTask;
    }
}
