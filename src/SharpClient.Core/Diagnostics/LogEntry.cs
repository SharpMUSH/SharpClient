namespace SharpClient.Core.Diagnostics;

/// <summary>One parsed line of the diagnostics log; <paramref name="Detail"/> holds an attached stack trace.</summary>
public sealed record LogEntry(
    DateTimeOffset Timestamp,
    string Level,
    string Category,
    string Message,
    string? Detail);

/// <summary>The crash recorded by the previous run, surfaced at launch.</summary>
public sealed record CrashReport(
    DateTimeOffset Timestamp,
    string Source,
    string Message,
    string Detail);
