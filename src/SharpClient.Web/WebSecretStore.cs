using System.Collections.Concurrent;
using SharpClient.Core.Persistence;

namespace SharpClient.Web;

/// <summary>
/// In-memory <see cref="ISecretStore"/> for the Blazor Server preview host. This is a deliberate
/// design choice, not a stub: connect-string secrets (passwords) are held only for the lifetime of
/// the host process and never written to disk, so the localhost preview tool never leaves plaintext
/// credentials on the filesystem. Persistent, encrypted secret storage is the MAUI host's job —
/// <c>MauiSecretStore</c> uses the platform keystore via <c>SecureStorage.Default</c>. If durable
/// secrets are ever needed on the web host, back this with an OS keychain / DPAPI / data-protection
/// API — do NOT serialize secrets to a plaintext file alongside preferences.
/// </summary>
public sealed class WebSecretStore : ISecretStore
{
    private readonly ConcurrentDictionary<string, string> _store = new();

    public Task SetAsync(string key, string secret)
    {
        _store[key] = secret;
        return Task.CompletedTask;
    }

    public Task<string?> GetAsync(string key) =>
        Task.FromResult(_store.TryGetValue(key, out var value) ? value : null);

    public Task RemoveAsync(string key)
    {
        _store.TryRemove(key, out _);
        return Task.CompletedTask;
    }
}
