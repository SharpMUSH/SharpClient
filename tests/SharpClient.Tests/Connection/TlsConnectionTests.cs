using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using SharpClient.Core.Connection;
using TelnetNegotiationCore.Builders;

namespace SharpClient.Tests.Connection;

/// <summary>TLS worlds against a loopback server holding a self-signed certificate.</summary>
public sealed class TlsConnectionTests
{
    private ServiceProvider? _serviceProvider;

    private ITelnetInterpreterFactory CreateFactory()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTelnetClient();
        _serviceProvider = services.BuildServiceProvider();
        return _serviceProvider.GetRequiredService<ITelnetInterpreterFactory>();
    }

    [After(Test)]
    public async Task TearDown()
    {
        if (_serviceProvider is not null)
        {
            await _serviceProvider.DisposeAsync();
            _serviceProvider = null;
        }
    }

    [Test]
    public async Task SelfSignedCertificateIsAcceptedWhenAllowed()
    {
        using var certificate = CreateSelfSigned();
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        await using var connection = new TelnetConnection(CreateFactory())
        {
            UseTls = true,
            AllowInvalidCertificate = true,
        };
        var received = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.LineReceived += line => received.TrySetResult(line);

        var server = ServeOneLineAsync(listener, certificate, "Hello over TLS");
        await connection.ConnectAsync("127.0.0.1", ((IPEndPoint)listener.LocalEndpoint).Port);

        var line = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(line).IsEqualTo("Hello over TLS");
        await Assert.That(connection.State).IsEqualTo(ConnectionState.Connected);
        await connection.DisconnectAsync();
        await server.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Test]
    public async Task SelfSignedCertificateIsRejectedByDefault()
    {
        using var certificate = CreateSelfSigned();
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        await using var connection = new TelnetConnection(CreateFactory()) { UseTls = true };

        var server = ServeOneLineAsync(listener, certificate, "unreachable");
        await Assert.That(async () => await connection.ConnectAsync("127.0.0.1", ((IPEndPoint)listener.LocalEndpoint).Port))
            .Throws<AuthenticationException>();
        await Assert.That(connection.State).IsEqualTo(ConnectionState.Error);

        // The server's handshake fails once the client gives up; that is the expected outcome here.
        try
        {
            await server.WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (Exception ex) when (ex is AuthenticationException or IOException)
        {
        }
    }

    private static async Task ServeOneLineAsync(TcpListener listener, X509Certificate2 certificate, string line)
    {
        using var client = await listener.AcceptTcpClientAsync();
        await using var ssl = new SslStream(client.GetStream(), leaveInnerStreamOpen: false);
        await ssl.AuthenticateAsServerAsync(new SslServerAuthenticationOptions { ServerCertificate = certificate });
        await ssl.WriteAsync(Encoding.ASCII.GetBytes(line + "\r\n"));
        await ssl.FlushAsync();

        // Hold the connection until the client closes it, so the line is not raced by EOF.
        var buffer = new byte[256];
        try
        {
            while (await ssl.ReadAsync(buffer) > 0)
            {
            }
        }
        catch (IOException)
        {
        }
    }

    private static X509Certificate2 CreateSelfSigned()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=127.0.0.1", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var san = new SubjectAlternativeNameBuilder();
        san.AddIpAddress(IPAddress.Loopback);
        request.CertificateExtensions.Add(san.Build());
        using var created = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddDays(1));
        // Round-trip through PKCS#12 so the private key is usable by SslStream on every platform.
        return X509CertificateLoader.LoadPkcs12(created.Export(X509ContentType.Pfx), null);
    }
}
