using SharpClient.Core.Sessions;

namespace SharpClient.Tests.Sessions;

/// <summary>
/// Guards the fix for the "Collection was modified; enumeration operation may not execute" crash
/// that killed the WebView UI: the telnet read loop appends to Scrollback off the network thread
/// while Blazor enumerates it on the render thread. Session must hand out an immutable snapshot and
/// guard its backing list so concurrent appends never corrupt an in-flight enumeration.
/// </summary>
public sealed class ScrollbackConcurrencyTests
{
    [Test]
    public async Task EnumeratingScrollbackWhileLinesArriveDoesNotThrow()
    {
        const int lineCount = 3_000;

        var conn = new FakeTelnetConnection();
        await using var session = new Session(conn);

        // Writer: append a bounded number of lines, mimicking the network read loop. Bounded (not
        // "until cancelled") so the test terminates fast and the snapshot stays small even when the
        // whole suite runs in parallel — an unbounded writer let the list grow without limit and
        // timed out under load.
        var writer = Task.Run(() =>
        {
            for (var i = 0; i < lineCount; i++)
            {
                conn.Emit("line " + i);
            }
        });

        // Reader: enumerate the public Scrollback the way OutputView's @foreach does, continuously
        // until the writer finishes. Pre-fix this raced against Emit's _scrollback.Add and threw
        // InvalidOperationException ("Collection was modified").
        Exception? readerError = null;
        var reader = Task.Run(() =>
        {
            try
            {
                while (!writer.IsCompleted)
                {
                    var seen = 0;
                    foreach (var line in session.Scrollback)
                    {
                        seen += line.Segments.Count;
                    }
                }
            }
            catch (Exception ex)
            {
                readerError = ex;
            }
        });

        await Task.WhenAll(writer, reader);

        await Assert.That(readerError).IsNull();
        await Assert.That(session.Scrollback.Count).IsEqualTo(lineCount);
    }

    [Test]
    public async Task SnapshotIsDecoupledFromLaterAppends()
    {
        var conn = new FakeTelnetConnection();
        await using var session = new Session(conn);

        conn.Emit("first");
        var snapshot = session.Scrollback;
        var countAtSnapshot = snapshot.Count;

        conn.Emit("second");

        // The previously-returned snapshot must not grow when new lines arrive.
        await Assert.That(snapshot.Count).IsEqualTo(countAtSnapshot);
        await Assert.That(session.Scrollback.Count).IsEqualTo(countAtSnapshot + 1);
    }
}
