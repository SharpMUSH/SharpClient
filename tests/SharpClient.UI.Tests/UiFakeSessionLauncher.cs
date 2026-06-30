using SharpClient.Core.Domain;
using SharpClient.Core.Sessions;

namespace SharpClient.UI.Tests;

public sealed class UiFakeSessionLauncher : ISessionLauncher
{
    public int LaunchCount { get; private set; }

    /// <summary>When set, <see cref="LaunchAsync"/> throws this instead of returning a session.</summary>
    public Exception? ThrowOnLaunch { get; set; }

    public Task<ISession> LaunchAsync(World world, Character character, CancellationToken cancellationToken = default)
    {
        LaunchCount++;
        if (ThrowOnLaunch is not null)
        {
            return Task.FromException<ISession>(ThrowOnLaunch);
        }

        return Task.FromResult<ISession>(new UiFakeSession
        {
            CharacterName = character.Name,
            WorldName = world.Name,
        });
    }
}
