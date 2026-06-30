using SharpClient.Core.Connection;
using SharpClient.Core.Domain;
using SharpClient.Core.Persistence;

namespace SharpClient.Core.Sessions;

/// <summary>
/// Real <see cref="ISessionLauncher"/> shared by both the Blazor Server web host
/// and the MAUI Blazor Hybrid Android host. Opens a TCP telnet connection via
/// <see cref="ITelnetConnectionFactory"/> (which wraps TelnetNegotiationCore),
/// wraps it in a <see cref="Session"/>, and auto-sends the character's connect
/// string (resolved from <see cref="ISecretStore"/>) immediately after connecting.
/// Disposes the session if the secret-send fails.
/// </summary>
public sealed class TelnetSessionLauncher : ISessionLauncher
{
    private readonly ITelnetConnectionFactory _connFactory;
    private readonly ISecretStore _secrets;

    public TelnetSessionLauncher(ITelnetConnectionFactory connFactory, ISecretStore secrets)
    {
        _connFactory = connFactory;
        _secrets = secrets;
    }

    public async Task<ISession> LaunchAsync(
        World world,
        Character character,
        CancellationToken cancellationToken = default)
    {
        var connection = _connFactory.CreateConnection();
        var session = new Session(connection, character.Name, world.Name);

        await session.ConnectAsync(world.Host, world.Port, cancellationToken);

        try
        {
            if (character.ConnectSecretKey is { } key)
            {
                var secret = await _secrets.GetAsync(key);
                if (!string.IsNullOrWhiteSpace(secret))
                    await session.SendAsync(secret);
            }
        }
        catch
        {
            await session.DisposeAsync();
            throw;
        }

        return session;
    }
}
