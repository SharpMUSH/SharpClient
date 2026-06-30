using SharpClient.Core.Connection;
using SharpClient.Core.Sessions;

namespace SharpClient.UI.Tests;

public sealed class UiFakeSession : ISession
{
    public ConnectionState State { get; set; } = ConnectionState.Connected;
    public string CharacterName { get; set; } = string.Empty;
    public string WorldName { get; set; } = string.Empty;
    public IReadOnlyList<ScrollbackLine> Scrollback { get; set; } = [];
    public List<string> Sent { get; } = [];

    // Explicit no-op implementations satisfy the interface without CS0067.
    public event Action<ScrollbackLine>? LineAppended { add { } remove { } }
    public event Action<ConnectionState>? StateChanged { add { } remove { } }

    public Task ConnectAsync(string host, int port, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task SendAsync(string line)
    {
        Sent.Add(line);
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
