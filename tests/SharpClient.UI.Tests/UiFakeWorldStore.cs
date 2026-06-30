using SharpClient.Core.Domain;
using SharpClient.Core.Persistence;

namespace SharpClient.UI.Tests;

public sealed class UiFakeWorldStore : IWorldStore
{
    private readonly List<World> _worlds = [];

    public Task<IReadOnlyList<World>> GetWorldsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<World>>(_worlds.ToList());

    public Task AddWorldAsync(World world, CancellationToken cancellationToken = default)
    {
        _worlds.Add(world);
        return Task.CompletedTask;
    }

    public Task UpdateWorldAsync(World world, CancellationToken cancellationToken = default)
    {
        var index = _worlds.FindIndex(w => w.Id == world.Id);
        if (index >= 0)
        {
            _worlds[index] = world;
        }
        else
        {
            _worlds.Add(world);
        }
        return Task.CompletedTask;
    }

    public Task DeleteWorldAsync(Guid worldId, CancellationToken cancellationToken = default)
    {
        _worlds.RemoveAll(w => w.Id == worldId);
        return Task.CompletedTask;
    }
}
