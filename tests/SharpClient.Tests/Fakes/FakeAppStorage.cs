using SharpClient.Core.Platform;

namespace SharpClient.Tests.Fakes;

public sealed class FakeAppStorage : IAppStorage
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

    public string GetDatabasePath() => _path;
}
