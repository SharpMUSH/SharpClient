namespace SharpClient.Core.Diagnostics;

public sealed class FileLogReader : ILogReader
{
    private readonly FileLogStore _store;

    public FileLogReader(FileLogStore store) => _store = store;

    public bool IsAvailable => true;

    public Task<IReadOnlyList<LogEntry>> ReadAsync(int maxEntries = 500)
    {
        var entries = new List<LogEntry>();
        entries.AddRange(ReadFile(_store.BackupPath));
        entries.AddRange(ReadFile(_store.FilePath));

        var start = Math.Max(0, entries.Count - maxEntries);
        var newest = entries.GetRange(start, entries.Count - start);
        newest.Reverse();

        return Task.FromResult<IReadOnlyList<LogEntry>>(newest);
    }

    public Task<CrashReport?> GetPendingCrashAsync()
    {
        var marker = ReadFile(_store.CrashMarkerPath);
        return Task.FromResult(marker.Count == 0
            ? null
            : new CrashReport(marker[0].Timestamp, marker[0].Category, marker[0].Message, marker[0].Detail ?? string.Empty));
    }

    public Task DismissCrashAsync()
    {
        Delete(_store.CrashMarkerPath);
        return Task.CompletedTask;
    }

    public Task ClearAsync()
    {
        Delete(_store.FilePath);
        Delete(_store.BackupPath);
        return Task.CompletedTask;
    }

    private static IReadOnlyList<LogEntry> ReadFile(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return [];
            }

            // The writer may hold the file open; share aggressively rather than fail the read.
            using var stream = new FileStream(
                path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var text = new StreamReader(stream);
            return LogEntryParser.Parse(text.ReadToEnd());
        }
        catch
        {
            return [];
        }
    }

    private static void Delete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best effort: a locked file just means the viewer still shows it.
        }
    }
}
