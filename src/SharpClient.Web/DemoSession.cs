using SharpClient.Core.Connection;
using SharpClient.Core.Rendering;
using SharpClient.Core.Sessions;

using CoreSession = SharpClient.Core.Sessions.ISession;

namespace SharpClient.Web;

public sealed class DemoSession : CoreSession
{
    private readonly List<ScrollbackLine> _scrollback = [];

    public IReadOnlyList<ScrollbackLine> Scrollback => _scrollback;

    public event Action<ScrollbackLine>? LineAppended;

    // StateChanged not raised in demo; explicit no-op satisfies the interface without CS0067.
    public event Action<ConnectionState>? StateChanged { add { } remove { } }

    public ConnectionState State { get; set; } = ConnectionState.Connected;

    public string CharacterName { get; set; } = string.Empty;

    public string WorldName { get; set; } = string.Empty;

    public Task ConnectAsync(string host, int port, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task SendAsync(string line)
    {
        var echo = new ScrollbackLine(AnsiParser.Parse($"\u001b[90m> {line}\u001b[0m"));
        _scrollback.Add(echo);
        LineAppended?.Invoke(echo);
        return Task.CompletedTask;
    }

    public void AppendLine(string ansiLine)
    {
        var scrollbackLine = new ScrollbackLine(AnsiParser.Parse(ansiLine));
        _scrollback.Add(scrollbackLine);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
