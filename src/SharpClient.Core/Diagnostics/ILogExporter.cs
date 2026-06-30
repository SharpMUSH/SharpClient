namespace SharpClient.Core.Diagnostics;

/// <summary>
/// Exposes the on-device diagnostics log so the UI can offer an "export / share" affordance.
/// Implemented per-platform: the MAUI app shares the real log file via the OS share sheet, while
/// the Blazor Web host has no persistent log and uses <see cref="NoopLogExporter"/>.
/// </summary>
public interface ILogExporter
{
    /// <summary>True when a log file exists and can be exported on this platform.</summary>
    public bool IsAvailable { get; }

    /// <summary>Absolute path to the current log file, or null when unavailable.</summary>
    public string? LogPath { get; }

    /// <summary>Opens the platform share sheet (or equivalent) so the user can hand off the log.</summary>
    public Task ShareAsync();
}

/// <summary>No-op exporter for hosts without a persistent file log (e.g. the Web preview).</summary>
public sealed class NoopLogExporter : ILogExporter
{
    public bool IsAvailable => false;

    public string? LogPath => null;

    public Task ShareAsync() => Task.CompletedTask;
}
