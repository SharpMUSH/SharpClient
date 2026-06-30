using SharpClient.Core.Connection;
using SharpClient.Core.Sessions;

namespace SharpClient.Tests.Sessions;

public sealed class FakeSession : ISession
{
    public IReadOnlyList<ScrollbackLine> Scrollback { get; } = [];
    public event Action<ScrollbackLine>? LineAppended;
    public event Action<ConnectionState>? StateChanged;
    public ConnectionState State { get; set; } = ConnectionState.Connected;
    public bool Disposed { get; private set; }
    public List<string> Sent { get; } = [];

    public Task ConnectAsync(string host, int port, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task SendAsync(string line)
    {
        Sent.Add(line);
        return Task.CompletedTask;
    }

    public void RaiseState(ConnectionState state)
    {
        State = state;
        StateChanged?.Invoke(state);
    }

    public void Append(ScrollbackLine line) => LineAppended?.Invoke(line);

    public ValueTask DisposeAsync()
    {
        Disposed = true;
        return ValueTask.CompletedTask;
    }
}
