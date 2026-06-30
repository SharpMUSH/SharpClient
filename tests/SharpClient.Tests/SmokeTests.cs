namespace SharpClient.Tests;

public sealed class SmokeTests
{
    [Test]
    public async Task TestHarnessRuns()
    {
        var values = Enumerable.Range(1, 4).ToArray();

        await Assert.That(values.Sum()).IsEqualTo(10);
    }
}
