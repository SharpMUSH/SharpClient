using SharpClient.Core.Connection;
using SharpClient.Core.Domain;
using SharpClient.Core.Persistence;
using SharpClient.Core.Sessions;

namespace SharpClient.App.Services;

/// <summary>
/// Real <see cref="ISessionLauncher"/> that opens a TCP telnet connection using
/// TelnetNegotiationCore (via <see cref="ITelnetConnectionFactory"/>), wraps it
/// in a <see cref="Session"/>, and auto-sends the character's connect string
/// (resolved from <see cref="ISecretStore"/>) immediately after connecting.
/// <para>
/// <b>Note on architecture:</b> TelnetNegotiationCore's <c>ITelnetInterpreterFactory</c>
/// is NOT referenced directly here because its Stateless dependency causes a Roslyn
/// metadata-loading failure under the <c>net10.0-android</c> TFM. Instead this class
/// uses <see cref="ITelnetConnectionFactory"/>, a Core-level abstraction implemented
/// by <c>TelnetConnectionFactory</c> (which lives in Core and compiles under the
/// plain <c>net10.0</c> TFM where TNC types are fully accessible).
/// </para>
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
                {
                    await session.SendAsync(secret);
                }
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
