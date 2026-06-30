namespace SharpClient.Tests.Fakes;

public sealed class FakesSmokeTests
{
    [Test]
    public async Task FakeSecretStoreSetThenGetReturnsValue()
    {
        var store = new FakeSecretStore();
        await store.SetAsync("mykey", "mysecret");

        var result = await store.GetAsync("mykey");

        await Assert.That(result).IsEqualTo("mysecret");
    }

    [Test]
    public async Task FakeSecretStoreRemoveThenGetReturnsNull()
    {
        var store = new FakeSecretStore();
        await store.SetAsync("mykey", "mysecret");
        await store.RemoveAsync("mykey");

        var result = await store.GetAsync("mykey");

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task FakeAppStorageGetDatabasePathReturnsNonEmptyString()
    {
        var storage = new FakeAppStorage();

        await Assert.That(storage.GetDatabasePath()).IsNotEmpty();
    }

    [Test]
    public async Task FakeAppStorageTwoInstancesReturnDifferentPaths()
    {
        var a = new FakeAppStorage();
        var b = new FakeAppStorage();

        await Assert.That(a.GetDatabasePath()).IsNotEqualTo(b.GetDatabasePath());
    }

    [Test]
    public async Task FakeNotifierNotifyAsyncRecordsMessage()
    {
        var notifier = new FakeNotifier();
        await notifier.NotifyAsync("x");

        await Assert.That(notifier.Messages).Contains("x");
    }
}
