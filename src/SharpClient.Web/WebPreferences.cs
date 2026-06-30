using SharpClient.Core.Platform;

namespace SharpClient.Web;

public sealed class WebPreferences : IPreferences
{
    private readonly Dictionary<string, string> _store = [];

    public string GetString(string key, string defaultValue) =>
        _store.TryGetValue(key, out var v) ? v : defaultValue;

    public void SetString(string key, string value) => _store[key] = value;

    public int GetInt(string key, int defaultValue) =>
        _store.TryGetValue(key, out var v) && int.TryParse(v, out var i) ? i : defaultValue;

    public void SetInt(string key, int value) =>
        _store[key] = value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public bool GetBool(string key, bool defaultValue) =>
        _store.TryGetValue(key, out var v) && bool.TryParse(v, out var b) ? b : defaultValue;

    public void SetBool(string key, bool value) => _store[key] = value.ToString();
}
