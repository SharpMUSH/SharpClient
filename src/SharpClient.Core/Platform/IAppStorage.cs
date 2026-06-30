namespace SharpClient.Core.Platform;

public interface IAppStorage
{
    public string GetDatabasePath();   // absolute path to the SQLite db file
}
