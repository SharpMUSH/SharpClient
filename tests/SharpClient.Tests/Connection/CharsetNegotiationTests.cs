using System.Text;
using Microsoft.Extensions.DependencyInjection;
using SharpClient.Core.Connection;
using TelnetNegotiationCore.Builders;

namespace SharpClient.Tests.Connection;

/// <summary>
/// Drives a real Telnet CHARSET (RFC 2066) handshake over the loopback server and
/// asserts the client both negotiates UTF-8 and uses it for sending and receiving.
/// </summary>
public sealed class CharsetNegotiationTests
{
    private const byte Iac = 255;
    private const byte Will = 251;
    private const byte Do = 253;
    private const byte Sb = 250;
    private const byte Se = 240;
    private const byte Charset = 42;
    private const byte Request = 1;
    private const byte Accepted = 2;

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
    public async Task NegotiatesUtf8AndRoundTripsNonAscii()
    {
        await using var server = new LoopbackServer();
        await using var connection = new TelnetConnection(CreateFactory());

        var received = new TaskCompletionSource<string>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        connection.LineReceived += line => received.TrySetResult(line);

        var accept = server.AcceptAsync();
        await connection.ConnectAsync("127.0.0.1", server.Port);
        await accept;

        // Server side of CHARSET negotiation: offer the option, then REQUEST a list
        // whose separator is ';'. The client prefers UTF-8 and must ACCEPT it.
        var negotiation = new List<byte> { Iac, Will, Charset, Iac, Sb, Charset, Request };
        negotiation.AddRange(Encoding.ASCII.GetBytes(";UTF-8;US-ASCII"));
        negotiation.AddRange([Iac, Se]);
        await server.SendBytesAsync([.. negotiation]);

        // The negotiated encoding should converge to UTF-8 (codepage 65001).
        await WaitUntilAsync(
            () => connection.CurrentEncoding?.CodePage == 65001,
            TimeSpan.FromSeconds(5));
        await Assert.That(connection.CurrentEncoding!.CodePage).IsEqualTo(65001);

        // Drain the client's negotiation reply, accumulating until the CHARSET ACCEPTED
        // subnegotiation appears (the reply may arrive across several reads).
        var reply = new List<byte>();
        var replyDeadline = DateTime.UtcNow + TimeSpan.FromSeconds(3);
        while (!ContainsSequence([.. reply], [Iac, Sb, Charset, Accepted])
               && DateTime.UtcNow < replyDeadline)
        {
            try
            {
                reply.AddRange(await server.ReadAvailableBytesAsync(TimeSpan.FromMilliseconds(200)));
            }
            catch (OperationCanceledException) { }
        }

        await Assert.That(ContainsSequence([.. reply], [Iac, Sb, Charset, Accepted])).IsTrue();

        // Sending: a non-ASCII line must be UTF-8 encoded on the wire.
        await connection.SendAsync("café");
        var sentBytes = await server.ReadAvailableBytesAsync(TimeSpan.FromSeconds(2));
        await Assert.That(Encoding.UTF8.GetString(sentBytes).Contains("café")).IsTrue();
        // 'é' is 0xC3 0xA9 in UTF-8 (a single 0xE9 byte would mean Latin-1 was used).
        await Assert.That(ContainsSequence(sentBytes, [0xC3, 0xA9])).IsTrue();

        // Receiving: UTF-8 bytes from the server must decode back to the original text.
        await server.SendBytesAsync(Encoding.UTF8.GetBytes("déjà vu\r\n"));
        var line = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(line).IsEqualTo("déjà vu");
    }

    private static async Task WaitUntilAsync(Func<bool> predicate, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        while (!predicate())
        {
            cts.Token.ThrowIfCancellationRequested();
            await Task.Delay(20, cts.Token);
        }
    }

    private static bool ContainsSequence(byte[] haystack, byte[] needle)
    {
        for (var i = 0; i + needle.Length <= haystack.Length; i++)
        {
            var match = true;
            for (var j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j])
                {
                    match = false;
                    break;
                }
            }

            if (match)
            {
                return true;
            }
        }

        return false;
    }
}
