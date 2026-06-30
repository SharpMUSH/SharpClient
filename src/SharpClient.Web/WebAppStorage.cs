using SharpClient.Core.Platform;

namespace SharpClient.Web;

public sealed class WebAppStorage : IAppStorage
{
    private readonly string _path;

    public WebAppStorage(IWebHostEnvironment environment)
    {
        var dir = Path.Combine(environment.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "sharpclient.db");
    }

    public string GetDatabasePath() => _path;
}
