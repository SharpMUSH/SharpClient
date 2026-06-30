namespace SharpClient.Core.Persistence;

public interface ISecretStore
{
    public Task SetAsync(string key, string secret);
    public Task<string?> GetAsync(string key);
    public Task RemoveAsync(string key);
}
