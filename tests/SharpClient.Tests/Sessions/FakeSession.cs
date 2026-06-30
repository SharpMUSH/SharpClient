using SharpClient.Core.Connection;
using SharpClient.Core.Sessions;

namespace SharpClient.Tests.Sessions;

public sealed class FakeSession : ISession
{
    public IReadOnlyList<ScrollbackLine> Scrollback { get; } = [];
    public event Action<ScrollbackLine>? LineAppended;
    public event Action<ConnectionState>? StateChanged;
    public ConnectionState State { get; set; } = ConnectionState.Connected;
    public string CharacterName { get; set; } = string.Empty;
    public string WorldName { get; set; } = string.Empty;
    public Guid WorldId { get; set; } = Guid.Empty;
    public Guid CharacterId { get; set; } = Guid.Empty;
    public bool Disposed { get; private set; }
    public List<string> Sent { get; } = [];
    public List<(int Cols, int Rows)> WindowSizesSent { get; } = [];

    public Task ConnectAsync(string host, int port, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task SendAsync(string line)
    {
        Sent.Add(line);
        return Task.CompletedTask;
    }

    public Task SendWindowSizeAsync(int cols, int rows)
    {
        WindowSizesSent.Add((cols, rows));
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
