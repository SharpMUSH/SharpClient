using SharpClient.Core.Domain;

namespace SharpClient.Core.Persistence;

public interface IWorldStore
{
    public Task<IReadOnlyList<World>> GetWorldsAsync(CancellationToken cancellationToken = default);
    public Task AddWorldAsync(World world, CancellationToken cancellationToken = default);
    public Task UpdateWorldAsync(World world, CancellationToken cancellationToken = default);
    public Task DeleteWorldAsync(Guid worldId, CancellationToken cancellationToken = default);
}
