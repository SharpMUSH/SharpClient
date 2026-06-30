namespace SharpClient.Core.Platform;

public interface INotifier
{
    public Task NotifyAsync(string message);
}
