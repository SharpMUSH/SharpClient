using Microsoft.Extensions.Logging;

namespace SharpClient.App.Services;

/// <summary>
/// Routes <c>ILogger</c> output (including Blazor's framework logs — an unhandled component exception
/// is logged at <see cref="LogLevel.Error"/> before the "An unhandled error has occurred" UI appears)
/// into the shared <see cref="FileLogStore"/>. Entries at or above the configured minimum level are kept.
/// </summary>
public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly FileLogStore _store;
    private readonly LogLevel _minLevel;

    public FileLoggerProvider(FileLogStore store, LogLevel minLevel = LogLevel.Information)
    {
        _store = store;
        _minLevel = minLevel;
    }

    public ILogger CreateLogger(string categoryName) => new FileLogger(_store, categoryName, _minLevel);

    public void Dispose()
    {
    }

    private sealed class FileLogger : ILogger
    {
        private readonly FileLogStore _store;
        private readonly string _category;
        private readonly LogLevel _minLevel;

        public FileLogger(FileLogStore store, string category, LogLevel minLevel)
        {
            _store = store;
            _category = category;
            _minLevel = minLevel;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= _minLevel && logLevel != LogLevel.None;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            _store.Append(logLevel.ToString(), _category, formatter(state, exception), exception);
        }
    }
}
