using SharpClient.Core.Persistence;

namespace SharpClient.Core.Presentation;

/// <summary>A single full-text history match, decorated with a human-readable character label.</summary>
public sealed record HistorySearchResult(string Line, string CharacterLabel, long Sequence);

/// <summary>
/// Drives the history-search screen: takes a free-text query, runs it against the FTS5-backed
/// <see cref="ISessionHistory"/>, and resolves each hit's <see cref="HistoryHit.CharacterId"/> to a
/// "Character @ World" label via <see cref="IWorldStore"/>. Mirrors the other presentation
/// view-models (synchronous state + a <see cref="Changed"/> event the Razor layer subscribes to).
/// </summary>
public sealed class HistorySearchViewModel
{
    private readonly ISessionHistory _history;
    private readonly IWorldStore _worldStore;

    public HistorySearchViewModel(ISessionHistory history, IWorldStore worldStore)
    {
        _history = history;
        _worldStore = worldStore;
    }

    /// <summary>Raised whenever query, results, or busy state change.</summary>
    public event Action? Changed;

    public string Query { get; private set; } = string.Empty;

    public IReadOnlyList<HistorySearchResult> Results { get; private set; } = [];

    public bool IsSearching { get; private set; }

    /// <summary>True once at least one search has run (so the UI can distinguish "no results" from "not searched yet").</summary>
    public bool HasSearched { get; private set; }

    public void SetQuery(string query)
    {
        if (query == Query)
        {
            return;
        }

        Query = query;
        Changed?.Invoke();
    }

    public async Task SearchAsync(CancellationToken cancellationToken = default)
    {
        var query = Query.Trim();
        if (query.Length == 0)
        {
            Results = [];
            HasSearched = true;
            Changed?.Invoke();
            return;
        }

        IsSearching = true;
        Changed?.Invoke();

        try
        {
            var labels = await GetCharacterLabelsAsync(cancellationToken);
            var hits = await _history.SearchAsync(query, cancellationToken: cancellationToken);

            Results = [.. hits.Select(h => new HistorySearchResult(
                h.Line,
                labels.TryGetValue(h.CharacterId, out var label) ? label : "Unknown character",
                h.Sequence))];
        }
        finally
        {
            IsSearching = false;
            HasSearched = true;
            Changed?.Invoke();
        }
    }

    public void Clear()
    {
        Query = string.Empty;
        Results = [];
        HasSearched = false;
        Changed?.Invoke();
    }

    private async Task<Dictionary<Guid, string>> GetCharacterLabelsAsync(CancellationToken cancellationToken)
    {
        // Rebuild the label map on each fresh search so newly-added characters resolve, but reuse a
        // cached map within a single search invocation.
        var worlds = await _worldStore.GetWorldsAsync(cancellationToken);
        var map = new Dictionary<Guid, string>();
        foreach (var world in worlds)
        {
            foreach (var character in world.Characters)
            {
                map[character.Id] = string.IsNullOrEmpty(world.Name)
                    ? character.Name
                    : $"{character.Name} @ {world.Name}";
            }
        }

        return map;
    }
}
