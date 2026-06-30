using SharpClient.Core.Domain;
using SharpClient.Core.Sessions;

namespace SharpClient.UI.Tests;

public sealed class UiFakeSessionLauncher : ISessionLauncher
{
    public int LaunchCount { get; private set; }

    public Task<ISession> LaunchAsync(World world, Character character, CancellationToken cancellationToken = default)
    {
        LaunchCount++;
        return Task.FromResult<ISession>(new UiFakeSession
        {
            CharacterName = character.Name,
            WorldName = world.Name,
        });
    }
}
