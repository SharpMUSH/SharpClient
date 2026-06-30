using SharpClient.Core.Platform;

namespace SharpClient.Tests.Fakes;

public sealed class FakePreferences : IPreferences
{
    public Dictionary<string, string> Store { get; } = [];

    public string GetString(string key, string defaultValue) =>
        Store.TryGetValue(key, out var v) ? v : defaultValue;

    public void SetString(string key, string value) => Store[key] = value;

    public int GetInt(string key, int defaultValue) =>
        Store.TryGetValue(key, out var v) && int.TryParse(v, out var i) ? i : defaultValue;

    public void SetInt(string key, int value) => Store[key] = value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public bool GetBool(string key, bool defaultValue) =>
        Store.TryGetValue(key, out var v) && bool.TryParse(v, out var b) ? b : defaultValue;

    public void SetBool(string key, bool value) => Store[key] = value.ToString();
}
