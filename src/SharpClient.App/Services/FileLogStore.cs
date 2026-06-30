using System.Globalization;
using System.Text;
using Microsoft.Maui.Storage;

namespace SharpClient.App.Services;

/// <summary>
/// Thread-safe, append-only diagnostics log written to a file under the app's private data directory,
/// with simple size-based rotation (current file + one rolled backup). It is the single sink for both
/// <see cref="FileLoggerProvider"/> (framework/app <c>ILogger</c> output) and the global
/// unhandled-exception hooks, so a crash that takes the process down still leaves its stack trace on
/// disk for the user to export and hand back.
/// </summary>
public sealed class FileLogStore
{
    // Keep the log small enough to share over chat but large enough to hold the lead-up to a crash.
    private const long MaxBytes = 512 * 1024;

    private readonly object _gate = new();

    /// <summary>Absolute path to the active log file.</summary>
    public string FilePath { get; }

    public FileLogStore()
    {
        var dir = Path.Combine(FileSystem.AppDataDirectory, "logs");
        Directory.CreateDirectory(dir);
        FilePath = Path.Combine(dir, "sharpclient.log");
    }

    /// <summary>Appends a single timestamped entry. Never throws — logging must not crash the app.</summary>
    public void Append(string level, string category, string message, Exception? ex = null)
    {
        var sb = new StringBuilder(256);
        sb.Append(DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz", CultureInfo.InvariantCulture));
        sb.Append(" [").Append(level).Append("] ");
        if (!string.IsNullOrEmpty(category))
        {
            sb.Append(category).Append(": ");
        }
        sb.Append(message);
        if (ex is not null)
        {
            sb.Append('\n').Append(ex);
        }
        sb.Append('\n');
        Write(sb.ToString());
    }

    /// <summary>Records an unhandled exception captured by one of the global hooks.</summary>
    public void WriteException(string source, Exception? ex)
        => Append("CRASH", source, ex?.Message ?? "(no exception object)", ex);

    private void Write(string text)
    {
        lock (_gate)
        {
            try
            {
                RotateIfNeeded();
                File.AppendAllText(FilePath, text);
            }
            catch
            {
                // Swallow: a failed log write must never propagate into the running app.
            }
        }
    }

    private void RotateIfNeeded()
    {
        try
        {
            var fi = new FileInfo(FilePath);
            if (!fi.Exists || fi.Length <= MaxBytes)
            {
                return;
            }

            var backup = FilePath + ".1";
            if (File.Exists(backup))
            {
                File.Delete(backup);
            }
            File.Move(FilePath, backup);
        }
        catch
        {
            // If rotation fails, fall through and keep appending to the existing file.
        }
    }
}
