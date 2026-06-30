using Microsoft.Extensions.Logging;
using SharpClient.Core.Platform;

namespace SharpClient.Web;

/// <summary>
/// Minimal <see cref="INotifier"/> for the Blazor Server web host.
/// Logs each notification via <see cref="ILogger"/>. Real push/toast
/// integration can replace this implementation without touching callers.
/// </summary>
public sealed partial class WebNotifier : INotifier
{
    private readonly ILogger<WebNotifier> _logger;

    public WebNotifier(ILogger<WebNotifier> logger) => _logger = logger;

    public Task NotifyAsync(string message)
    {
        LogNotification(_logger, message);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Notification: {Message}")]
    private static partial void LogNotification(ILogger logger, string message);
}
