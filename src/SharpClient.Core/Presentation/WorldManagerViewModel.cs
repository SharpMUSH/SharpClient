using SharpClient.Core.Domain;
using SharpClient.Core.Persistence;
using SharpClient.Core.Sessions;

namespace SharpClient.Core.Presentation;

public sealed class WorldManagerViewModel
{
    private readonly IWorldStore _store;
    private readonly ISecretStore _secrets;
    private readonly ISessionManager _sessions;
    private readonly ISessionLauncher _launcher;

    private IReadOnlyList<World> _worlds = [];

    public WorldManagerViewModel(
        IWorldStore store,
        ISecretStore secrets,
        ISessionManager sessions,
        ISessionLauncher launcher)
    {
        _store = store;
        _secrets = secrets;
        _sessions = sessions;
        _launcher = launcher;
    }

    public IReadOnlyList<World> Worlds => _worlds;

    public bool HasWorlds => _worlds.Count > 0;

    public event Action? Changed;

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        _worlds = await _store.GetWorldsAsync(cancellationToken);
        RaiseChanged();
    }

    public async Task AddWorldAsync(string name, string host, int port, CancellationToken cancellationToken = default)
    {
        var world = new World { Name = name, Host = host, Port = port };
        await _store.AddWorldAsync(world, cancellationToken);
        await LoadAsync(cancellationToken);
    }

    public async Task UpdateWorldAsync(World world, CancellationToken cancellationToken = default)
    {
        await _store.UpdateWorldAsync(world, cancellationToken);
        await LoadAsync(cancellationToken);
    }

    public async Task DeleteWorldAsync(Guid worldId, CancellationToken cancellationToken = default)
    {
        await _store.DeleteWorldAsync(worldId, cancellationToken);
        await LoadAsync(cancellationToken);
    }

    public async Task AddCharacterAsync(World world, string name, string? connectString, CancellationToken cancellationToken = default)
    {
        var character = new Character { WorldId = world.Id, Name = name };
        await ApplyConnectSecretAsync(character, connectString);
        world.Characters.Add(character);
        await _store.UpdateWorldAsync(world, cancellationToken);
        await LoadAsync(cancellationToken);
    }

    public async Task UpdateCharacterAsync(World world, Character character, string name, string? connectString, CancellationToken cancellationToken = default)
    {
        character.Name = name;
        await ApplyConnectSecretAsync(character, connectString);
        await _store.UpdateWorldAsync(world, cancellationToken);
        await LoadAsync(cancellationToken);
    }

    public async Task DeleteCharacterAsync(World world, Character character, CancellationToken cancellationToken = default)
    {
        world.Characters.RemoveAll(c => c.Id == character.Id);
        if (character.ConnectSecretKey is { } key)
        {
            await _secrets.RemoveAsync(key);
        }
        await _store.UpdateWorldAsync(world, cancellationToken);
        await LoadAsync(cancellationToken);
    }

    public async Task ConnectAsync(World world, Character character, CancellationToken cancellationToken = default)
    {
        var session = await _launcher.LaunchAsync(world, character, cancellationToken);
        _sessions.Add(session);
    }

    // Stores the connect string as a secret keyed by the character id; never writes it
    // into a domain text field. A blank connect string leaves any existing secret in
    // place (edit cannot clear a secret — functional over fancy).
    private async Task ApplyConnectSecretAsync(Character character, string? connectString)
    {
        if (string.IsNullOrWhiteSpace(connectString))
        {
            return;
        }

        var key = character.ConnectSecretKey ?? $"connect:{character.Id:N}";
        character.ConnectSecretKey = key;
        await _secrets.SetAsync(key, connectString);
    }

    private void RaiseChanged() => Changed?.Invoke();
}
