using Microsoft.Extensions.Logging;
using Plugin.LocalNotification;
using Plugin.LocalNotification.Core.Models;
using SharpClient.Core.Platform;

namespace SharpClient.App.Services;

/// <summary>
/// <see cref="INotifier"/> backed by Plugin.LocalNotification. Posts a real OS local notification
/// for each "Notify" trigger action and degrades gracefully (log only) when notifications are
/// unsupported or the user has denied permission.
/// </summary>
public sealed partial class MauiNotifier : INotifier
{
    internal const string AlertChannelId = "sharpclient_alerts";

    private static int _nextId = 1_000;

    private readonly ILogger<MauiNotifier> _logger;

    public MauiNotifier(ILogger<MauiNotifier> logger) => _logger = logger;

    public async Task NotifyAsync(string message)
    {
        LogNotification(_logger, message);

        try
        {
            var center = LocalNotificationCenter.Current;
            if (!center.IsSupported)
            {
                return;
            }

            var allowed = await center.RequestNotificationPermission();
            if (!allowed)
            {
                return;
            }

            var request = new NotificationRequest
            {
                NotificationId = Interlocked.Increment(ref _nextId),
                Title = "SharpClient",
                Description = message,
                Android = new() { ChannelId = AlertChannelId },
            };

            await center.Show(request);
        }
        catch (Exception ex)
        {
            LogNotifyFailed(_logger, ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Notification: {Message}")]
    private static partial void LogNotification(ILogger logger, string message);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to post local notification.")]
    private static partial void LogNotifyFailed(ILogger logger, Exception exception);
}
