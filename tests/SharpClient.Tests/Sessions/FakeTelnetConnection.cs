using SharpClient.Core.Connection;

namespace SharpClient.Tests.Sessions;

public sealed class FakeTelnetConnection : ITelnetConnection
{
    public event Action<string>? LineReceived;
    public event Action<ConnectionState>? StateChanged;
    public event Action<GmcpMessage>? GmcpReceived;
    public event Action<NegotiationEvent>? NegotiationReceived;

    public ConnectionState State { get; private set; } = ConnectionState.Disconnected;
    public List<string> Sent { get; } = [];

    public Task ConnectAsync(string host, int port, CancellationToken cancellationToken = default)
    {
        RaiseState(ConnectionState.Connected);
        return Task.CompletedTask;
    }

    public Task SendAsync(string line)
    {
        Sent.Add(line);
        return Task.CompletedTask;
    }

    public Task DisconnectAsync()
    {
        RaiseState(ConnectionState.Disconnected);
        return Task.CompletedTask;
    }

    public void Emit(string line) => LineReceived?.Invoke(line);

    public void EmitGmcp(GmcpMessage msg) => GmcpReceived?.Invoke(msg);

    public void EmitNegotiation(NegotiationEvent ev) => NegotiationReceived?.Invoke(ev);

    public void RaiseState(ConnectionState state)
    {
        State = state;
        StateChanged?.Invoke(state);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
