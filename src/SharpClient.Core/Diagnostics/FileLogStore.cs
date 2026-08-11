using System.Globalization;
using System.Text;

namespace SharpClient.Core.Diagnostics;

/// <summary>
/// Thread-safe, append-only diagnostics log written to a caller-supplied directory, with simple
/// size-based rotation (current file + one rolled backup). It is the single sink for both
/// <see cref="FileLoggerProvider"/> (framework/app <c>ILogger</c> output) and the global
/// unhandled-exception hooks, so a crash that takes the process down still leaves its stack trace on
/// disk.
/// </summary>
public sealed class FileLogStore
{
    // Keep the log small enough to share over chat but large enough to hold the lead-up to a crash.
    private const long MaxBytes = 512 * 1024;

    private readonly object _gate = new();

    /// <summary>Absolute path to the active log file.</summary>
    public string FilePath { get; }

    /// <summary>Absolute path to the single rolled-over backup.</summary>
    public string BackupPath { get; }

    /// <summary>
    /// Sidecar holding the most recent crash block. Checking one small file at launch is cheaper than
    /// scanning the log, and it survives rotation.
    /// </summary>
    public string CrashMarkerPath { get; }

    public FileLogStore(string logDirectory)
    {
        Directory.CreateDirectory(logDirectory);
        FilePath = Path.Combine(logDirectory, "sharpclient.log");
        BackupPath = FilePath + ".1";
        CrashMarkerPath = Path.Combine(logDirectory, "last-crash.txt");
    }

    /// <summary>Appends a single timestamped entry. Never throws — logging must not crash the app.</summary>
    public void Append(string level, string category, string message, Exception? ex = null)
        => Write(FormatEntry(level, category, message, ex));

    /// <summary>Records an unhandled exception captured by one of the global hooks.</summary>
    public void WriteException(string source, Exception? ex)
    {
        var block = FormatEntry("CRASH", source, ex?.Message ?? "(no exception object)", ex);
        Write(block);
        WriteCrashMarker(block);
    }

    private static string FormatEntry(string level, string category, string message, Exception? ex)
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
        return sb.ToString();
    }

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

    private void WriteCrashMarker(string block)
    {
        try
        {
            File.WriteAllText(CrashMarkerPath, block);
        }
        catch
        {
            // Same contract as the log write: recording a crash must not cause another one.
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

            if (File.Exists(BackupPath))
            {
                File.Delete(BackupPath);
            }

            File.Move(FilePath, BackupPath);
        }
        catch
        {
            // If rotation fails, fall through and keep appending to the existing file.
        }
    }
}
