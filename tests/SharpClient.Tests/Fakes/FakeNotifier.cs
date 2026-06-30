using SharpClient.Core.Platform;

namespace SharpClient.Tests.Fakes;

public sealed class FakeNotifier : INotifier
{
    public List<string> Messages { get; } = [];

    public Task NotifyAsync(string message)
    {
        Messages.Add(message);
        return Task.CompletedTask;
    }
}
