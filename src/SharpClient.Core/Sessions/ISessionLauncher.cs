using SharpClient.Core.Domain;

namespace SharpClient.Core.Sessions;

/// <summary>
/// Creates a started <see cref="ISession"/> for a character on a world. Implementations
/// own the transport choice (real telnet vs. a web/demo stand-in) so the Connect flow
/// never hard-depends on sockets.
/// </summary>
/// <remarks>
/// Implementations are ALSO responsible for auto-sending the character's connect string
/// after connecting: resolve <see cref="Character.ConnectSecretKey"/> via
/// <c>ISecretStore</c> and send the resolved value on connect. The returned session is
/// already started; its identity is the character/world names.
/// </remarks>
public interface ISessionLauncher
{
    public Task<ISession> LaunchAsync(World world, Character character, CancellationToken cancellationToken = default);
}
