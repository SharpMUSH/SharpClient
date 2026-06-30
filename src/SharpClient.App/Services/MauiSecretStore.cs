using Microsoft.Maui.Storage;
using SharpClient.Core.Persistence;

namespace SharpClient.App.Services;

/// <summary>
/// Persists secrets (e.g., character connect strings) using the platform's
/// secure storage (Android Keystore on Android, iOS Keychain on iOS).
/// </summary>
public sealed class MauiSecretStore : ISecretStore
{
    public Task SetAsync(string key, string secret) =>
        SecureStorage.Default.SetAsync(key, secret);

    public Task<string?> GetAsync(string key) =>
        SecureStorage.Default.GetAsync(key);

    public Task RemoveAsync(string key)
    {
        SecureStorage.Default.Remove(key);
        return Task.CompletedTask;
    }
}
