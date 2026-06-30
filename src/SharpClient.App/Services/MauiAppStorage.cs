using SharpClient.Core.Platform;

namespace SharpClient.App.Services;

/// <summary>
/// Resolves the SQLite database path to the MAUI application data directory.
/// The directory is guaranteed to exist by FileSystem.AppDataDirectory.
/// </summary>
public sealed class MauiAppStorage : IAppStorage
{
    private readonly string _dbPath;

    public MauiAppStorage()
    {
        _dbPath = Path.Combine(FileSystem.AppDataDirectory, "sharpclient.db");
    }

    public string GetDatabasePath() => _dbPath;
}
