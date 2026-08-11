using SharpClient.Core.Diagnostics;

namespace SharpClient.Tests.Diagnostics;

public sealed class FileLogStoreTests
{
    private static string NewTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sharpclient-tests", Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Test]
    public async Task ConstructorCreatesTheDirectory()
    {
        var dir = Path.Combine(NewTempDir(), "nested", "logs");

        var store = new FileLogStore(dir);

        await Assert.That(Directory.Exists(dir)).IsTrue();
        await Assert.That(store.FilePath).IsEqualTo(Path.Combine(dir, "sharpclient.log"));
    }

    [Test]
    public async Task AppendWritesLevelCategoryAndMessage()
    {
        var store = new FileLogStore(NewTempDir());

        store.Append("Information", "App", "hello");

        var text = File.ReadAllText(store.FilePath);
        await Assert.That(text).Contains("[Information]");
        await Assert.That(text).Contains("App: hello");
    }

    [Test]
    public async Task AppendOmitsTheCategorySeparatorWhenCategoryIsEmpty()
    {
        var store = new FileLogStore(NewTempDir());

        store.Append("Information", string.Empty, "bare");

        await Assert.That(File.ReadAllText(store.FilePath)).Contains("[Information] bare");
    }

    [Test]
    public async Task AppendIncludesExceptionDetailOnFollowingLines()
    {
        var store = new FileLogStore(NewTempDir());
        var ex = new InvalidOperationException("boom");

        store.Append("Error", "Session", "failed", ex);

        var text = File.ReadAllText(store.FilePath);
        await Assert.That(text).Contains("Session: failed");
        await Assert.That(text).Contains("System.InvalidOperationException: boom");
    }

    [Test]
    public async Task WriteExceptionRecordsACrashLevelEntry()
    {
        var store = new FileLogStore(NewTempDir());

        store.WriteException("AppDomain", new InvalidOperationException("boom"));

        var text = File.ReadAllText(store.FilePath);
        await Assert.That(text).Contains("[CRASH]");
        await Assert.That(text).Contains("AppDomain: boom");
    }

    [Test]
    public async Task WriteExceptionToleratesANullException()
    {
        var store = new FileLogStore(NewTempDir());

        store.WriteException("AppDomain", null);

        await Assert.That(File.ReadAllText(store.FilePath)).Contains("(no exception object)");
    }

    [Test]
    public async Task OversizeLogRotatesIntoTheBackupFile()
    {
        var store = new FileLogStore(NewTempDir());
        var big = new string('x', 600 * 1024);

        store.Append("Information", "App", big);
        store.Append("Information", "App", "after rotation");

        await Assert.That(File.Exists(store.BackupPath)).IsTrue();
        await Assert.That(File.ReadAllText(store.BackupPath)).Contains(big);
        await Assert.That(File.ReadAllText(store.FilePath)).Contains("after rotation");
        await Assert.That(File.ReadAllText(store.FilePath)).DoesNotContain(big);
    }

    [Test]
    public async Task RotationReplacesAnExistingBackup()
    {
        var store = new FileLogStore(NewTempDir());
        var big = new string('x', 600 * 1024);

        store.Append("Information", "App", "first generation");
        store.Append("Information", "App", big);
        store.Append("Information", "App", "second generation trigger");
        store.Append("Information", "App", big);
        store.Append("Information", "App", "final");

        await Assert.That(File.ReadAllText(store.BackupPath)).DoesNotContain("first generation");
        await Assert.That(File.ReadAllText(store.FilePath)).Contains("final");
    }
}
