using Microsoft.Extensions.Logging;
using SharpClient.Core.Platform;

namespace SharpClient.App.Services;

/// <summary>
/// Stub implementation of <see cref="INotifier"/> that logs notifications via
/// <see cref="ILogger"/>. Push / local-notification support is deferred —
/// replace this with a real implementation backed by the MAUI
/// LocalNotifications plugin when needed.
/// </summary>
public sealed partial class MauiNotifier : INotifier
{
    private readonly ILogger<MauiNotifier> _logger;

    public MauiNotifier(ILogger<MauiNotifier> logger) => _logger = logger;

    public Task NotifyAsync(string message)
    {
        LogNotification(_logger, message);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Notification: {Message}")]
    private static partial void LogNotification(ILogger logger, string message);
}
