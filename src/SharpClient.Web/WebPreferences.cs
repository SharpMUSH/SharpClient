using System.Globalization;
using System.Text.Json;
using SharpClient.Core.Platform;

namespace SharpClient.Web;

/// <summary>
/// File-backed <see cref="IPreferences"/> for the Blazor Server preview host. Settings are kept in
/// memory for fast synchronous access (the <see cref="IPreferences"/> contract is synchronous) and
/// written through to <c>App_Data/preferences.json</c> on every change, so they survive page reloads
/// and host restarts — matching the MAUI host, where <c>MauiPreferences</c> uses
/// <c>Preferences.Default</c>. Only non-sensitive UI settings flow through here; secrets go through
/// <see cref="WebSecretStore"/>.
/// </summary>
public sealed class WebPreferences : IPreferences
{
    private readonly string _path;
    private readonly object _gate = new();
    private readonly Dictionary<string, string> _store;

    public WebPreferences(IAppStorage storage)
    {
        var dir = Path.GetDirectoryName(storage.GetDatabasePath()) ?? ".";
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "preferences.json");
        _store = Load(_path);
    }

    public string GetString(string key, string defaultValue)
    {
        lock (_gate)
        {
            return _store.TryGetValue(key, out var v) ? v : defaultValue;
        }
    }

    public void SetString(string key, string value) => Set(key, value);

    public int GetInt(string key, int defaultValue)
    {
        lock (_gate)
        {
            return _store.TryGetValue(key, out var v)
                && int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i)
                ? i : defaultValue;
        }
    }

    public void SetInt(string key, int value) =>
        Set(key, value.ToString(CultureInfo.InvariantCulture));

    public bool GetBool(string key, bool defaultValue)
    {
        lock (_gate)
        {
            return _store.TryGetValue(key, out var v) && bool.TryParse(v, out var b) ? b : defaultValue;
        }
    }

    public void SetBool(string key, bool value) =>
        Set(key, value ? "true" : "false");

    private void Set(string key, string value)
    {
        lock (_gate)
        {
            _store[key] = value;
            Save();
        }
    }

    // Caller holds _gate.
    private void Save()
    {
        try
        {
            File.WriteAllText(_path, JsonSerializer.Serialize(_store));
        }
        catch (IOException)
        {
            // Best-effort: a failed write just means this change isn't persisted; the in-memory
            // value still applies for the current session.
        }
    }

    private static Dictionary<string, string> Load(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var loaded = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                if (loaded is not null)
                {
                    return loaded;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            // Corrupt or unreadable preferences file — start fresh rather than crash the host.
        }

        return [];
    }
}
