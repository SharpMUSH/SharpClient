using TelnetNegotiationCore.Protocols;

namespace SharpClient.Core.Connection;

/// <summary>
/// NAWS that keeps the latest window size until the server enables NAWS, then reports it. The base
/// plugin drops a report made before <c>DO NAWS</c> (RFC 1073 forbids sending it), and nothing would
/// report again until the next resize: the size the UI measured at connect, or the one carried over an
/// auto-reconnect, never reached the server. Registers under <see cref="NAWSProtocol"/>'s type, so
/// <c>GetPlugin&lt;NAWSProtocol&gt;()</c> finds it.
/// </summary>
internal sealed class WindowSizeProtocol : NAWSProtocol
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private (int Width, int Height)? _requested;
    private (int Width, int Height)? _reported;

    public WindowSizeProtocol((int Width, int Height)? initial = null) =>
        _requested = initial is { } size ? (Clamp(size.Width), Clamp(size.Height)) : null;

    public async ValueTask ReportAsync(int width, int height)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            _requested = (Clamp(width), Clamp(height));
            await ReportIfReadyAsync().ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    protected override async ValueTask OnNegotiationChangedAsync(bool isNegotiated)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            // A fresh agreement needs its own initial report even if the size has not changed.
            if (!isNegotiated)
            {
                _reported = null;
            }
            else
            {
                await ReportIfReadyAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    protected override async ValueTask OnDisposeAsync()
    {
        await base.OnDisposeAsync().ConfigureAwait(false);
        _gate.Dispose();
    }

    // RFC 1073 fields are 16-bit unsigned; TNC throws outside that range rather than wrapping.
    private static int Clamp(int value) => Math.Clamp(value, 0, MaxWindowDimension);

    private async ValueTask ReportIfReadyAsync()
    {
        if (!IsNegotiated || !WindowSizeReportingEnabled || _requested is not { } size || _reported == size)
        {
            return;
        }

        await SendWindowSizeAsync(size.Width, size.Height).ConfigureAwait(false);
        _reported = size;
    }
}
