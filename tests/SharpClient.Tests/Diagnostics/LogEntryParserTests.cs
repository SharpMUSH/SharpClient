using SharpClient.Core.Diagnostics;

namespace SharpClient.Tests.Diagnostics;

public sealed class LogEntryParserTests
{
    [Test]
    public async Task ParsesTimestampLevelCategoryAndMessage()
    {
        var entries = LogEntryParser.Parse("2026-08-10 23:41:02.123 -05:00 [Information] App: started\n");

        await Assert.That(entries).Count().IsEqualTo(1);
        await Assert.That(entries[0].Level).IsEqualTo("Information");
        await Assert.That(entries[0].Category).IsEqualTo("App");
        await Assert.That(entries[0].Message).IsEqualTo("started");
        await Assert.That(entries[0].Detail).IsNull();
        await Assert.That(entries[0].Timestamp.Offset).IsEqualTo(TimeSpan.FromHours(-5));
        await Assert.That(entries[0].Timestamp.Year).IsEqualTo(2026);
    }

    [Test]
    public async Task ParsesAnEntryWithNoCategory()
    {
        var entries = LogEntryParser.Parse("2026-08-10 23:41:02.123 -05:00 [Information] bare message\n");

        await Assert.That(entries[0].Category).IsEqualTo(string.Empty);
        await Assert.That(entries[0].Message).IsEqualTo("bare message");
    }

    [Test]
    public async Task AMessageContainingColonSpaceIsNotMistakenForACategory()
    {
        var entries = LogEntryParser.Parse("2026-08-10 23:41:02.123 -05:00 [Information] NAWS fit: cols=78\n");

        await Assert.That(entries[0].Category).IsEqualTo(string.Empty);
        await Assert.That(entries[0].Message).IsEqualTo("NAWS fit: cols=78");
    }

    [Test]
    public async Task ContinuationLinesBecomeTheDetail()
    {
        const string text = """
            2026-08-10 23:41:02.123 -05:00 [CRASH] AppDomain: boom
            System.InvalidOperationException: boom
               at SharpClient.Core.Sessions.Session.SendAsync(String line)
               at SharpClient.UI.Components.InputBar.SendAsync()

            """;

        var entries = LogEntryParser.Parse(text);

        await Assert.That(entries).Count().IsEqualTo(1);
        await Assert.That(entries[0].Message).IsEqualTo("boom");
        await Assert.That(entries[0].Detail).Contains("System.InvalidOperationException: boom");
        await Assert.That(entries[0].Detail).Contains("at SharpClient.UI.Components.InputBar.SendAsync()");
    }

    [Test]
    public async Task MultipleEntriesAreReturnedOldestFirst()
    {
        const string text = """
            2026-08-10 23:41:02.123 -05:00 [Information] App: first
            2026-08-10 23:41:03.456 -05:00 [Warning] App: second

            """;

        var entries = LogEntryParser.Parse(text);

        await Assert.That(entries).Count().IsEqualTo(2);
        await Assert.That(entries[0].Message).IsEqualTo("first");
        await Assert.That(entries[1].Message).IsEqualTo("second");
    }

    [Test]
    public async Task TextBeforeTheFirstHeaderIsDiscarded()
    {
        const string text = """
               at SomeTruncatedStackFrame()
            2026-08-10 23:41:02.123 -05:00 [Information] App: real entry

            """;

        var entries = LogEntryParser.Parse(text);

        await Assert.That(entries).Count().IsEqualTo(1);
        await Assert.That(entries[0].Message).IsEqualTo("real entry");
    }

    [Test]
    public async Task TextWithNoHeadersYieldsNoEntries()
    {
        var entries = LogEntryParser.Parse("garbage\nmore garbage\n");

        await Assert.That(entries).IsEmpty();
    }

    [Test]
    public async Task EmptyTextYieldsNoEntries()
    {
        await Assert.That(LogEntryParser.Parse(string.Empty)).IsEmpty();
    }

    [Test]
    public async Task CorruptTimestampIsSkippedWithoutThrowing()
    {
        const string text = """
            2026-08-10 23:41:02.123 -05:00 [Information] App: valid before
            2026-13-01 10:00:00.000 +00:00 [Information] App: corrupt month
            2026-08-11 12:00:00.456 +02:00 [Warning] Log: valid after

            """;

        var entries = LogEntryParser.Parse(text);

        await Assert.That(entries).Count().IsEqualTo(2);
        await Assert.That(entries[0].Message).IsEqualTo("valid before");
        await Assert.That(entries[1].Message).IsEqualTo("valid after");
    }
}
