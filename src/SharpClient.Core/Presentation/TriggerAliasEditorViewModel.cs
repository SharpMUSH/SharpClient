using SharpClient.Core.Domain;
using SharpClient.Core.Persistence;

namespace SharpClient.Core.Presentation;

public sealed class TriggerAliasEditorViewModel
{
    private readonly IWorldStore _store;
    private Guid _worldId;
    private World? _world;
    private Guid? _characterScopeId;

    public TriggerAliasEditorViewModel(IWorldStore store) => _store = store;

    // ── Scope ─────────────────────────────────────────────────────────────

    /// <summary>null means world scope; non-null is the character's Id.</summary>
    public Guid? CharacterScopeId => _characterScopeId;

    /// <summary>Display-friendly scope label.</summary>
    public string ScopeName => CurrentCharacter?.Name ?? "World";

    /// <summary>Characters available to choose as a scope.</summary>
    public IReadOnlyList<Character> Characters => _world?.Characters ?? (IReadOnlyList<Character>)[];

    private Character? CurrentCharacter =>
        _characterScopeId is { } id ? _world?.Characters.Find(c => c.Id == id) : null;

    private List<TriggerRule> ScopedTriggers =>
        CurrentCharacter?.Triggers ?? _world?.Triggers ?? [];

    private List<AliasRule> ScopedAliases =>
        CurrentCharacter?.Aliases ?? _world?.Aliases ?? [];

    /// <summary>
    /// Switch the active scope.  Pass <c>null</c> for world scope,
    /// or a character's <see cref="Character.Id"/> for character scope.
    /// </summary>
    public void SetScope(Guid? characterId)
    {
        _characterScopeId = characterId;
        RaiseChanged();
    }

    // ── Exposed lists ─────────────────────────────────────────────────────

    public IReadOnlyList<TriggerRule> Triggers => ScopedTriggers;
    public IReadOnlyList<AliasRule> Aliases => ScopedAliases;

    public event Action? Changed;

    // ── Load / Set ────────────────────────────────────────────────────────

    public async Task LoadAsync(Guid worldId, CancellationToken ct = default)
    {
        _worldId = worldId;
        var worlds = await _store.GetWorldsAsync(ct);
        _world = worlds.FirstOrDefault(w => w.Id == worldId);
        // if the previously selected character is no longer present, fall back to world
        if (_characterScopeId is { } id && _world?.Characters.Find(c => c.Id == id) is null)
            _characterScopeId = null;
        RaiseChanged();
    }

    public void SetWorld(World world)
    {
        _worldId = world.Id;
        _world = world;
        // if the previously selected character is no longer present, fall back to world
        if (_characterScopeId is { } id && _world.Characters.Find(c => c.Id == id) is null)
            _characterScopeId = null;
        RaiseChanged();
    }

    // ── Triggers ──────────────────────────────────────────────────────────

    public async Task AddTriggerAsync(TriggerRule rule, CancellationToken ct = default)
    {
        if (_world is null) return;
        ScopedTriggers.Add(rule);
        await _store.UpdateWorldAsync(_world, ct);
        await LoadAsync(_worldId, ct);
    }

    public async Task UpdateTriggerAsync(TriggerRule rule, CancellationToken ct = default)
    {
        if (_world is null) return;
        var list = ScopedTriggers;
        var idx = list.FindIndex(t => t.Id == rule.Id);
        if (idx >= 0) list[idx] = rule;
        await _store.UpdateWorldAsync(_world, ct);
        await LoadAsync(_worldId, ct);
    }

    public async Task DeleteTriggerAsync(Guid id, CancellationToken ct = default)
    {
        if (_world is null) return;
        ScopedTriggers.RemoveAll(t => t.Id == id);
        await _store.UpdateWorldAsync(_world, ct);
        await LoadAsync(_worldId, ct);
    }

    public async Task ToggleTriggerAsync(Guid id, CancellationToken ct = default)
    {
        if (_world is null) return;
        var rule = ScopedTriggers.FirstOrDefault(t => t.Id == id);
        if (rule is null) return;
        rule.Enabled = !rule.Enabled;
        await _store.UpdateWorldAsync(_world, ct);
        await LoadAsync(_worldId, ct);
    }

    // ── Aliases ───────────────────────────────────────────────────────────

    public async Task AddAliasAsync(AliasRule rule, CancellationToken ct = default)
    {
        if (_world is null) return;
        ScopedAliases.Add(rule);
        await _store.UpdateWorldAsync(_world, ct);
        await LoadAsync(_worldId, ct);
    }

    public async Task UpdateAliasAsync(AliasRule rule, CancellationToken ct = default)
    {
        if (_world is null) return;
        var list = ScopedAliases;
        var idx = list.FindIndex(a => a.Id == rule.Id);
        if (idx >= 0) list[idx] = rule;
        await _store.UpdateWorldAsync(_world, ct);
        await LoadAsync(_worldId, ct);
    }

    public async Task DeleteAliasAsync(Guid id, CancellationToken ct = default)
    {
        if (_world is null) return;
        ScopedAliases.RemoveAll(a => a.Id == id);
        await _store.UpdateWorldAsync(_world, ct);
        await LoadAsync(_worldId, ct);
    }

    public async Task ToggleAliasAsync(Guid id, CancellationToken ct = default)
    {
        if (_world is null) return;
        var alias = ScopedAliases.FirstOrDefault(a => a.Id == id);
        if (alias is null) return;
        alias.Enabled = !alias.Enabled;
        await _store.UpdateWorldAsync(_world, ct);
        await LoadAsync(_worldId, ct);
    }

    private void RaiseChanged() => Changed?.Invoke();
}
