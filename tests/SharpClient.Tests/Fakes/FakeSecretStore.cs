using SharpClient.Core.Persistence;

namespace SharpClient.Tests.Fakes;

public sealed class FakeSecretStore : ISecretStore
{
    private readonly Dictionary<string, string> _store = new();

    public Task SetAsync(string key, string secret)
    {
        _store[key] = secret;
        return Task.CompletedTask;
    }

    public Task<string?> GetAsync(string key) =>
        Task.FromResult(_store.TryGetValue(key, out var value) ? value : null);

    public Task RemoveAsync(string key)
    {
        _store.Remove(key);
        return Task.CompletedTask;
    }
}
