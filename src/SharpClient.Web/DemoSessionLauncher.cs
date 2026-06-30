using SharpClient.Core.Connection;
using SharpClient.Core.Domain;
using SharpClient.Core.Persistence;
using SharpClient.Core.Sessions;

using CoreSession = SharpClient.Core.Sessions.ISession;

namespace SharpClient.Web;

public sealed class DemoSessionLauncher : ISessionLauncher
{
    private readonly ISecretStore _secrets;

    public DemoSessionLauncher(ISecretStore secrets) => _secrets = secrets;

    public async Task<CoreSession> LaunchAsync(World world, Character character, CancellationToken cancellationToken = default)
    {
        var session = new DemoSession
        {
            CharacterName = character.Name,
            WorldName = world.Name,
            State = ConnectionState.Connected,
        };

        session.AppendLine($"[1m[32m✓ Connected to {world.Name}[0m ([90m{world.Host}:{world.Port}[0m)");

        if (character.ConnectSecretKey is { } key)
        {
            var connect = await _secrets.GetAsync(key);
            if (!string.IsNullOrWhiteSpace(connect))
            {
                // Auto-send the connect string on connect (masked in the demo feed).
                session.AppendLine("[90m» sent connect string[0m");
            }
        }

        session.AppendLine($"[90mWelcome, {character.Name}. Type [4mhelp[0m to begin.[0m");
        return session;
    }
}
