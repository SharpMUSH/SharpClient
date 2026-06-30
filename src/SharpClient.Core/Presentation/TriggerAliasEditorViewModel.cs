using SharpClient.Core.Domain;
using SharpClient.Core.Persistence;

namespace SharpClient.Core.Presentation;

public sealed class TriggerAliasEditorViewModel
{
    private readonly IWorldStore _store;
    private Guid _worldId;
    private World? _world;

    public TriggerAliasEditorViewModel(IWorldStore store) => _store = store;

    public IReadOnlyList<TriggerRule> Triggers => _world?.Triggers ?? (IReadOnlyList<TriggerRule>)[];
    public IReadOnlyList<AliasRule> Aliases => _world?.Aliases ?? (IReadOnlyList<AliasRule>)[];

    public event Action? Changed;

    public async Task LoadAsync(Guid worldId, CancellationToken ct = default)
    {
        _worldId = worldId;
        var worlds = await _store.GetWorldsAsync(ct);
        _world = worlds.FirstOrDefault(w => w.Id == worldId);
        RaiseChanged();
    }

    public void SetWorld(World world)
    {
        _worldId = world.Id;
        _world = world;
        RaiseChanged();
    }

    // ── Triggers ──────────────────────────────────────────────────────────

    public async Task AddTriggerAsync(TriggerRule rule, CancellationToken ct = default)
    {
        _world!.Triggers.Add(rule);
        await _store.UpdateWorldAsync(_world, ct);
        await LoadAsync(_worldId, ct);
    }

    public async Task UpdateTriggerAsync(TriggerRule rule, CancellationToken ct = default)
    {
        var idx = _world!.Triggers.FindIndex(t => t.Id == rule.Id);
        if (idx >= 0) _world.Triggers[idx] = rule;
        await _store.UpdateWorldAsync(_world, ct);
        await LoadAsync(_worldId, ct);
    }

    public async Task DeleteTriggerAsync(Guid id, CancellationToken ct = default)
    {
        _world!.Triggers.RemoveAll(t => t.Id == id);
        await _store.UpdateWorldAsync(_world, ct);
        await LoadAsync(_worldId, ct);
    }

    public async Task ToggleTriggerAsync(Guid id, CancellationToken ct = default)
    {
        var rule = _world!.Triggers.FirstOrDefault(t => t.Id == id);
        if (rule is not null) rule.Enabled = !rule.Enabled;
        await _store.UpdateWorldAsync(_world!, ct);
        await LoadAsync(_worldId, ct);
    }

    // ── Aliases ───────────────────────────────────────────────────────────

    public async Task AddAliasAsync(AliasRule rule, CancellationToken ct = default)
    {
        _world!.Aliases.Add(rule);
        await _store.UpdateWorldAsync(_world, ct);
        await LoadAsync(_worldId, ct);
    }

    public async Task UpdateAliasAsync(AliasRule rule, CancellationToken ct = default)
    {
        var idx = _world!.Aliases.FindIndex(a => a.Id == rule.Id);
        if (idx >= 0) _world.Aliases[idx] = rule;
        await _store.UpdateWorldAsync(_world, ct);
        await LoadAsync(_worldId, ct);
    }

    public async Task DeleteAliasAsync(Guid id, CancellationToken ct = default)
    {
        _world!.Aliases.RemoveAll(a => a.Id == id);
        await _store.UpdateWorldAsync(_world, ct);
        await LoadAsync(_worldId, ct);
    }

    public async Task ToggleAliasAsync(Guid id, CancellationToken ct = default)
    {
        var alias = _world!.Aliases.FirstOrDefault(a => a.Id == id);
        if (alias is not null) alias.Enabled = !alias.Enabled;
        await _store.UpdateWorldAsync(_world!, ct);
        await LoadAsync(_worldId, ct);
    }

    private void RaiseChanged() => Changed?.Invoke();
}
