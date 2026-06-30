namespace SharpClient.Core.Persistence;

public sealed record HistoryHit(Guid CharacterId, string Line, long Sequence);

public interface ISessionHistory
{
    public Task AppendAsync(Guid characterId, string line, CancellationToken cancellationToken = default);
    public Task<IReadOnlyList<HistoryHit>> SearchAsync(string query, int limit = 100, CancellationToken cancellationToken = default);
}
