using System.IO.Compression;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using SharpClient.Core.Connection;
using TelnetNegotiationCore.Builders;

namespace SharpClient.Tests.Connection;

/// <summary>
/// Wire-level checks for the options SharpClient answers but never starts: MCCP2, NEW-ENVIRON (MNES)
/// and MXP's SUPPORT/VERSION queries.
/// </summary>
public sealed class ProtocolAnswerTests
{
    private const byte Iac = 255;
    private const byte Will = 251;
    private const byte Do = 253;
    private const byte Sb = 250;
    private const byte Se = 240;
    private const byte Compress2 = 86;
    private const byte NewEnviron = 39;
    private const byte Mxp = 91;

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
    public async Task ConnectingSendsNoOptionRequest()
    {
        await using var server = new LoopbackServer();
        await using var connection = new TelnetConnection(CreateFactory());

        var accept = server.AcceptAsync();
        await connection.ConnectAsync("127.0.0.1", server.Port);
        await accept;

        // A DO the server never offered sits in a non-telnet server's line buffer and gets glued to
        // the auto-login (the failure SharpMUTerm hit). Only WILLs for our own options may go out.
        var sent = await WireHelpers.DrainAsync(server, TimeSpan.FromMilliseconds(400));
        for (var i = 0; i + 1 < sent.Length; i++)
        {
            if (sent[i] == Iac)
            {
                await Assert.That(sent[i + 1]).IsNotEqualTo(Do);
            }
        }
    }

    [Test]
    public async Task Mccp2CompressedOutputIsDecompressed()
    {
        await using var server = new LoopbackServer();
        await using var connection = new TelnetConnection(CreateFactory());
        var received = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.LineReceived += line => received.TrySetResult(line);

        var accept = server.AcceptAsync();
        await connection.ConnectAsync("127.0.0.1", server.Port);
        await accept;

        await server.SendBytesAsync([Iac, Will, Compress2]);
        var reply = await WireHelpers.ReadUntilAsync(server, [Iac, Do, Compress2], TimeSpan.FromSeconds(3));
        await Assert.That(WireHelpers.IndexOf(reply, [Iac, Do, Compress2])).IsGreaterThanOrEqualTo(0);

        using var compressed = new MemoryStream();
        await using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
        {
            await zlib.WriteAsync(Encoding.ASCII.GetBytes("compressed hello\r\n"));
            await zlib.FlushAsync();
        }

        await server.SendBytesAsync([Iac, Sb, Compress2, Iac, Se, .. compressed.ToArray()]);

        var line = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(line).IsEqualTo("compressed hello");
    }

    [Test]
    public async Task NewEnvironAnswersWithClientName()
    {
        await using var server = new LoopbackServer();
        await using var connection = new TelnetConnection(CreateFactory());

        var accept = server.AcceptAsync();
        await connection.ConnectAsync("127.0.0.1", server.Port);
        await accept;

        await server.SendBytesAsync([Iac, Do, NewEnviron]);
        await WireHelpers.ReadUntilAsync(server, [Iac, Will, NewEnviron], TimeSpan.FromSeconds(3));
        await server.SendBytesAsync([Iac, Sb, NewEnviron, 1, Iac, Se]); // SEND, everything

        var reply = await WireHelpers.ReadUntilAsync(server, [Iac, Sb, NewEnviron, 0], TimeSpan.FromSeconds(3), requireEnd: true);
        var start = WireHelpers.IndexOf(reply, [Iac, Sb, NewEnviron, 0]);
        await Assert.That(start).IsGreaterThanOrEqualTo(0);

        var text = Encoding.ASCII.GetString(reply, start, reply.Length - start);
        await Assert.That(text).Contains("CLIENT_NAME\u0001SharpClient");
        await Assert.That(text).Contains("TERMINAL_TYPE\u0001XTERM");
    }

    [Test]
    public async Task MxpSupportAndVersionAreAnswered()
    {
        await using var server = new LoopbackServer();
        await using var connection = new TelnetConnection(CreateFactory());

        var accept = server.AcceptAsync();
        await connection.ConnectAsync("127.0.0.1", server.Port);
        await accept;

        await server.SendBytesAsync([Iac, Will, Mxp]);
        await WireHelpers.ReadUntilAsync(server, [Iac, Do, Mxp], TimeSpan.FromSeconds(3));
        await server.SendBytesAsync([Iac, Sb, Mxp, Iac, Se]);
        await server.SendBytesAsync(Encoding.ASCII.GetBytes("\e[1z<SUPPORT>\r\n"));

        var supports = Encoding.ASCII.GetString(
            await WireHelpers.ReadUntilAsync(server, Encoding.ASCII.GetBytes("+image"), TimeSpan.FromSeconds(3)));
        await Assert.That(supports).Contains("<SUPPORTS");
        await Assert.That(supports).Contains("+send");
        await Assert.That(supports).Contains("+image");

        await server.SendBytesAsync(Encoding.ASCII.GetBytes("\e[1z<VERSION>\r\n"));
        var version = Encoding.ASCII.GetString(
            await WireHelpers.ReadUntilAsync(server, Encoding.ASCII.GetBytes("CLIENT=SharpClient"), TimeSpan.FromSeconds(3)));
        await Assert.That(version).Contains("<VERSION MXP=1.0 CLIENT=SharpClient>");
    }
}

/// <summary>Byte helpers shared by the loopback wire tests.</summary>
internal static class WireHelpers
{
    private const byte Iac = 255;
    private const byte Se = 240;

    public static async Task<byte[]> ReadUntilAsync(
        LoopbackServer server, byte[] marker, TimeSpan timeout, bool requireEnd = false)
    {
        var buffer = new List<byte>();
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var at = IndexOf([.. buffer], marker);
            if (at >= 0 && (!requireEnd || IndexOf([.. buffer.Skip(at + marker.Length)], [Iac, Se]) >= 0))
            {
                return [.. buffer];
            }

            try
            {
                buffer.AddRange(await server.ReadAvailableBytesAsync(TimeSpan.FromMilliseconds(200)));
            }
            catch (OperationCanceledException) { }
        }

        return [.. buffer];
    }

    public static async Task<byte[]> DrainAsync(LoopbackServer server, TimeSpan window)
    {
        var buffer = new List<byte>();
        var deadline = DateTime.UtcNow + window;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                buffer.AddRange(await server.ReadAvailableBytesAsync(TimeSpan.FromMilliseconds(100)));
            }
            catch (OperationCanceledException) { }
        }

        return [.. buffer];
    }

    public static int IndexOf(byte[] haystack, byte[] needle)
    {
        for (var i = 0; i + needle.Length <= haystack.Length; i++)
        {
            if (haystack.AsSpan(i, needle.Length).SequenceEqual(needle))
            {
                return i;
            }
        }

        return -1;
    }
}
