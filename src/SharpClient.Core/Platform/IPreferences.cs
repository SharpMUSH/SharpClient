namespace SharpClient.Core.Platform;

public interface IPreferences
{
    public string GetString(string key, string defaultValue);
    public void SetString(string key, string value);
    public int GetInt(string key, int defaultValue);
    public void SetInt(string key, int value);
    public bool GetBool(string key, bool defaultValue);
    public void SetBool(string key, bool value);
}
