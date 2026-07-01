namespace SharpClient.Core.Connection;

/// <summary>
/// Tunes the auto-reconnect behaviour of <see cref="TelnetConnection"/> after an
/// unexpected socket drop. Delays follow a bounded exponential backoff
/// (<see cref="InitialDelay"/> doubled per attempt, capped at <see cref="MaxDelay"/>).
/// Timings are injectable so tests can keep the suite fast.
/// </summary>
public sealed record ReconnectOptions
{
    /// <summary>
    /// Maximum number of reconnect attempts before giving up with <see cref="ConnectionState.Error"/>.
    /// Defaults high so a session survives long connectivity gaps (tunnels, rural dead zones on a
    /// drive): with the backoff capped at <see cref="MaxDelay"/>, ~60 attempts is roughly half an
    /// hour of retrying. A returning network can also resume sooner via
    /// <see cref="TelnetConnection.ForceReconnectAsync"/> (e.g. driven by an Android connectivity callback).
    /// </summary>
    public int MaxAttempts { get; init; } = 60;

    /// <summary>Delay before the first reconnect attempt.</summary>
    public TimeSpan InitialDelay { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>Upper bound on the backoff delay.</summary>
    public TimeSpan MaxDelay { get; init; } = TimeSpan.FromSeconds(30);

    public static ReconnectOptions Default { get; } = new();

    /// <summary>Backoff delay for a 1-based attempt number.</summary>
    public TimeSpan DelayFor(int attempt)
    {
        var factor = Math.Pow(2, Math.Max(0, attempt - 1));
        var millis = InitialDelay.TotalMilliseconds * factor;
        var capped = Math.Min(millis, MaxDelay.TotalMilliseconds);
        return TimeSpan.FromMilliseconds(capped);
    }
}
