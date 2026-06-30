using SharpClient.Core.Domain;
using SharpClient.Core.Persistence;

namespace SharpClient.Tests.Fakes;

public sealed class FakeWorldStore : IWorldStore
{
    private readonly List<World> _worlds = [];

    public int AddCount { get; private set; }
    public int UpdateCount { get; private set; }
    public int DeleteCount { get; private set; }

    public Task<IReadOnlyList<World>> GetWorldsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<World>>(_worlds.ToList());

    public Task AddWorldAsync(World world, CancellationToken cancellationToken = default)
    {
        AddCount++;
        _worlds.Add(world);
        return Task.CompletedTask;
    }

    public Task UpdateWorldAsync(World world, CancellationToken cancellationToken = default)
    {
        UpdateCount++;
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
        DeleteCount++;
        _worlds.RemoveAll(w => w.Id == worldId);
        return Task.CompletedTask;
    }
}
