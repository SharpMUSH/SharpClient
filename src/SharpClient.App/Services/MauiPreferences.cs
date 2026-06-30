using Microsoft.Maui.Storage;
using AppPrefs = SharpClient.Core.Platform.IPreferences;

namespace SharpClient.App.Services;

public sealed class MauiPreferences : AppPrefs
{
    public string GetString(string key, string defaultValue) =>
        Preferences.Default.Get(key, defaultValue);

    public void SetString(string key, string value) =>
        Preferences.Default.Set(key, value);

    public int GetInt(string key, int defaultValue) =>
        Preferences.Default.Get(key, defaultValue);

    public void SetInt(string key, int value) =>
        Preferences.Default.Set(key, value);

    public bool GetBool(string key, bool defaultValue) =>
        Preferences.Default.Get(key, defaultValue);

    public void SetBool(string key, bool value) =>
        Preferences.Default.Set(key, value);
}
