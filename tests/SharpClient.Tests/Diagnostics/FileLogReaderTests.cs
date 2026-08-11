using SharpClient.Core.Diagnostics;

namespace SharpClient.Tests.Diagnostics;

public sealed class FileLogReaderTests
{
    private static (FileLogStore store, FileLogReader reader) Build()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sharpclient-tests", Guid.NewGuid().ToString("n"));
        var store = new FileLogStore(dir);
        return (store, new FileLogReader(store));
    }

    [Test]
    public async Task ReadReturnsNewestFirst()
    {
        var (store, reader) = Build();
        store.Append("Information", "App", "first");
        store.Append("Information", "App", "second");

        var entries = await reader.ReadAsync();

        await Assert.That(entries).Count().IsEqualTo(2);
        await Assert.That(entries[0].Message).IsEqualTo("second");
        await Assert.That(entries[1].Message).IsEqualTo("first");
    }

    [Test]
    public async Task ReadReturnsEmptyWhenNothingHasBeenLogged()
    {
        var (_, reader) = Build();

        await Assert.That(await reader.ReadAsync()).IsEmpty();
    }

    [Test]
    public async Task ReadMergesTheRotatedBackupBeforeTheCurrentFile()
    {
        var (store, reader) = Build();
        store.Append("Information", "App", "oldest");
        store.Append("Information", "App", new string('x', 600 * 1024));
        store.Append("Information", "App", "newest");

        var entries = await reader.ReadAsync();

        await Assert.That(entries[0].Message).IsEqualTo("newest");
        await Assert.That(entries[^1].Message).IsEqualTo("oldest");
    }

    [Test]
    public async Task MaxEntriesKeepsTheNewest()
    {
        var (store, reader) = Build();
        for (var i = 0; i < 10; i++)
        {
            store.Append("Information", "App", $"entry {i}");
        }

        var entries = await reader.ReadAsync(maxEntries: 3);

        await Assert.That(entries).Count().IsEqualTo(3);
        await Assert.That(entries[0].Message).IsEqualTo("entry 9");
        await Assert.That(entries[2].Message).IsEqualTo("entry 7");
    }

    [Test]
    public async Task NoPendingCrashOnAFreshStore()
    {
        var (_, reader) = Build();

        await Assert.That(await reader.GetPendingCrashAsync()).IsNull();
    }

    [Test]
    public async Task WriteExceptionLeavesAPendingCrash()
    {
        var (store, reader) = Build();
        store.WriteException("AppDomain", new InvalidOperationException("boom"));

        var report = await reader.GetPendingCrashAsync();

        await Assert.That(report).IsNotNull();
        await Assert.That(report!.Source).IsEqualTo("AppDomain");
        await Assert.That(report.Message).IsEqualTo("boom");
        await Assert.That(report.Detail).Contains("System.InvalidOperationException");
    }

    [Test]
    public async Task DismissClearsThePendingCrashButKeepsTheLog()
    {
        var (store, reader) = Build();
        store.WriteException("AppDomain", new InvalidOperationException("boom"));

        await reader.DismissCrashAsync();

        await Assert.That(await reader.GetPendingCrashAsync()).IsNull();
        await Assert.That(await reader.ReadAsync()).IsNotEmpty();
    }

    [Test]
    public async Task ClearDeletesTheLogFilesButNotThePendingCrash()
    {
        var (store, reader) = Build();
        store.WriteException("AppDomain", new InvalidOperationException("boom"));

        await reader.ClearAsync();

        await Assert.That(await reader.ReadAsync()).IsEmpty();
        await Assert.That(File.Exists(store.FilePath)).IsFalse();
        await Assert.That(await reader.GetPendingCrashAsync()).IsNotNull();
    }

    [Test]
    public async Task NoopReaderIsUnavailableAndEmpty()
    {
        var reader = new NoopLogReader();

        await Assert.That(reader.IsAvailable).IsFalse();
        await Assert.That(await reader.ReadAsync()).IsEmpty();
        await Assert.That(await reader.GetPendingCrashAsync()).IsNull();
    }
}
