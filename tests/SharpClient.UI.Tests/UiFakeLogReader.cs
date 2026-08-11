using SharpClient.Core.Diagnostics;

namespace SharpClient.UI.Tests;

public sealed class UiFakeLogReader : ILogReader
{
    public List<LogEntry> Entries { get; } = [];
    public CrashReport? Pending { get; set; }
    public int ClearCalls { get; private set; }
    public int DismissCalls { get; private set; }

    public bool IsAvailable => true;

    public Task<IReadOnlyList<LogEntry>> ReadAsync(int maxEntries = 500) =>
        Task.FromResult<IReadOnlyList<LogEntry>>(Entries);

    public Task<CrashReport?> GetPendingCrashAsync() => Task.FromResult(Pending);

    public Task DismissCrashAsync()
    {
        DismissCalls++;
        Pending = null;
        return Task.CompletedTask;
    }

    public Task ClearAsync()
    {
        ClearCalls++;
        Entries.Clear();
        return Task.CompletedTask;
    }
}
