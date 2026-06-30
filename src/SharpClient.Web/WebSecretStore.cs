using System.Collections.Concurrent;
using SharpClient.Core.Persistence;

namespace SharpClient.Web;

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
