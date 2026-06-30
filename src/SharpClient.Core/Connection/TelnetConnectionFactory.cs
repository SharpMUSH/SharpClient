using TelnetNegotiationCore.Builders;
using TelnetNegotiationCore.Interpreters;
using TelnetNegotiationCore.Models;

namespace SharpClient.Core.Connection;

/// <summary>
/// Default implementation of <see cref="ITelnetConnectionFactory"/> that wraps
/// TelnetNegotiationCore's <c>ITelnetInterpreterFactory</c>. Placed in Core (not in
/// the MAUI App) so TNC types are compiled under <c>net10.0</c>, where they resolve
/// correctly.
/// </summary>
public sealed class TelnetConnectionFactory : ITelnetConnectionFactory
{
    private readonly ITelnetInterpreterFactory _factory;

    public TelnetConnectionFactory(ITelnetInterpreterFactory factory) => _factory = factory;

    public ITelnetConnection CreateConnection() => new TelnetConnection(_factory);
}
