using SharpClient.Core.Persistence;

namespace SharpClient.Tests.Fakes;

public sealed class FakeSessionHistory : ISessionHistory
{
    public List<(Guid CharacterId, string Line)> Appended { get; } = [];

    public Task AppendAsync(Guid characterId, string line, CancellationToken cancellationToken = default)
    {
        Appended.Add((characterId, line));
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<HistoryHit>> SearchAsync(string query, int limit = 100, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<HistoryHit>>([]);
}
