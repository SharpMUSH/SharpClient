using SharpClient.Core.Domain;
using SharpClient.Core.Sessions;
using SharpClient.Tests.Sessions;

namespace SharpClient.Tests.Fakes;

public sealed class FakeSessionLauncher : ISessionLauncher
{
    public int LaunchCount { get; private set; }
    public World? LastWorld { get; private set; }
    public Character? LastCharacter { get; private set; }
    public FakeSession Session { get; } = new();

    public Task<ISession> LaunchAsync(World world, Character character, CancellationToken cancellationToken = default)
    {
        LaunchCount++;
        LastWorld = world;
        LastCharacter = character;
        return Task.FromResult<ISession>(Session);
    }
}
