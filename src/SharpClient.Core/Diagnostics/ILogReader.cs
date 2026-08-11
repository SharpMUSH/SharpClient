namespace SharpClient.Core.Diagnostics;

/// <summary>
/// Reads the on-device diagnostics log back so the app can show it after a restart. Implemented per
/// host: the MAUI app reads the real files, the Blazor Web host has no persistent log and uses
/// <see cref="NoopLogReader"/>.
/// </summary>
public interface ILogReader
{
    /// <summary>True when a log exists on this platform and the viewer should be offered.</summary>
    public bool IsAvailable { get; }

    /// <summary>The most recent entries, newest first, across the current and rotated files.</summary>
    public Task<IReadOnlyList<LogEntry>> ReadAsync(int maxEntries = 500);

    /// <summary>The crash recorded by the previous run, or null if it exited cleanly.</summary>
    public Task<CrashReport?> GetPendingCrashAsync();

    /// <summary>Forgets the pending crash so the banner stops appearing.</summary>
    public Task DismissCrashAsync();

    /// <summary>Deletes the log and its rotated backup. Leaves any pending crash marker alone.</summary>
    public Task ClearAsync();
}

/// <summary>No-op reader for hosts without a persistent file log (e.g. the Web preview).</summary>
public sealed class NoopLogReader : ILogReader
{
    public bool IsAvailable => false;

    public Task<IReadOnlyList<LogEntry>> ReadAsync(int maxEntries = 500) =>
        Task.FromResult<IReadOnlyList<LogEntry>>([]);

    public Task<CrashReport?> GetPendingCrashAsync() => Task.FromResult<CrashReport?>(null);

    public Task DismissCrashAsync() => Task.CompletedTask;

    public Task ClearAsync() => Task.CompletedTask;
}
