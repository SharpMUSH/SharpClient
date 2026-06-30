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
    private string? _lastError;

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

    /// <summary>
    /// User-visible message describing the most recent connect failure, or null when
    /// there is nothing to show. Dismiss with <see cref="ClearError"/>.
    /// </summary>
    public string? LastError => _lastError;

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
        var world = _worlds.FirstOrDefault(w => w.Id == worldId);
        if (world is not null)
        {
            foreach (var character in world.Characters)
            {
                if (character.ConnectSecretKey is { } key)
                {
                    await _secrets.RemoveAsync(key);
                }
            }
        }
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
        // The launcher (and the telnet connect it wraps) throws on a bad host/port or an
        // offline server. Catch it here so the failure surfaces as a dismissible banner
        // instead of tearing through the Blazor circuit with no user feedback.
        try
        {
            var session = await _launcher.LaunchAsync(world, character, cancellationToken);
            _sessions.Add(session);
            ClearError();
        }
        catch (Exception ex)
        {
            SetError($"Couldn't connect to {world.Name}: {ex.Message}");
        }
    }

    /// <summary>Clears any surfaced connect error and notifies observers if one was set.</summary>
    public void ClearError()
    {
        if (_lastError is null)
        {
            return;
        }

        _lastError = null;
        RaiseChanged();
    }

    private void SetError(string message)
    {
        _lastError = message;
        RaiseChanged();
    }

    /// <summary>
    /// Returns the active session for a character, matched first by
    /// <see cref="ISession.CharacterId"/> (exact), then by character name as fallback.
    /// </summary>
    public ISession? ActiveSessionFor(Character character) =>
        _sessions.Sessions.FirstOrDefault(s => s.CharacterId == character.Id)
        ?? _sessions.Sessions.FirstOrDefault(s =>
            string.Equals(s.CharacterName, character.Name, StringComparison.Ordinal));

    /// <summary>
    /// Returns true if any character in the world currently has a live session.
    /// </summary>
    public bool IsWorldLive(World world) =>
        world.Characters.Any(c => ActiveSessionFor(c) is not null);

    /// <summary>
    /// Activates (makes current) the existing session for the given character.
    /// No-op if no session exists for that character.
    /// </summary>
    public void ActivateSession(Character character)
    {
        var session = ActiveSessionFor(character);
        if (session is not null)
        {
            _sessions.Activate(session);
        }
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
