namespace SharpClient.Core.Connection;

/// <summary>
/// Creates new <see cref="ITelnetConnection"/> instances. This abstraction lets
/// the MAUI App project create real telnet connections without a direct compile-time
/// reference to TelnetNegotiationCore's types (which do not resolve under the
/// <c>net10.0-android</c> TFM due to a Stateless assembly-version mismatch during
/// Roslyn metadata loading). The concrete implementation lives in Core so it is
/// compiled under the plain <c>net10.0</c> TFM where TNC types are fully accessible.
/// </summary>
public interface ITelnetConnectionFactory
{
    public ITelnetConnection CreateConnection();
}
