using System.Text;
using Microsoft.Extensions.DependencyInjection;
using SharpClient.Core.Connection;
using TelnetNegotiationCore.Builders;

namespace SharpClient.Tests.Connection;

/// <summary>
/// Wire-level checks for what TNC 2.8+ stopped doing on our behalf: naming the client in TTYPE, and
/// reporting the window size through the NAWS plugin once the server has asked for it.
/// </summary>
public sealed class ClientIdentityAndNawsTests
{
    private const byte Iac = 255;
    private const byte Do = 253;
    private const byte Sb = 250;
    private const byte Se = 240;
    private const byte Ttype = 24;
    private const byte Naws = 31;
    private const byte TtypeIs = 0;
    private const byte TtypeSend = 1;

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
    public async Task TerminalTypeNamesSharpClientThenXtermThenMtts()
    {
        await using var server = new LoopbackServer();
        await using var connection = new TelnetConnection(CreateFactory());

        var accept = server.AcceptAsync();
        await connection.ConnectAsync("127.0.0.1", server.Port);
        await accept;

        await server.SendBytesAsync([Iac, Do, Ttype]);
        var names = new List<string>();
        for (var i = 0; i < 3; i++)
        {
            await server.SendBytesAsync([Iac, Sb, Ttype, TtypeSend, Iac, Se]);
            names.Add(await ReadTtypeIsAsync(server));
        }

        await Assert.That(names[0]).IsEqualTo("SharpClient");
        await Assert.That(names[1]).IsEqualTo("XTERM");
        // MTTS 1 (ANSI) | 4 (UTF-8) | 8 (256 colours) from SharpClient, plus whatever the library
        // observes about its own stack; truecolour (256) must not be claimed.
        await Assert.That(names[2]).StartsWith("MTTS ");
        var bits = int.Parse(names[2]["MTTS ".Length..], System.Globalization.CultureInfo.InvariantCulture);
        await Assert.That(bits & 13).IsEqualTo(13);
        await Assert.That(bits & 256).IsEqualTo(0);
    }

    [Test]
    public async Task NawsIsSentAfterDoNawsAndClampedToSixteenBits()
    {
        await using var server = new LoopbackServer();
        await using var connection = new TelnetConnection(CreateFactory());

        var accept = server.AcceptAsync();
        await connection.ConnectAsync("127.0.0.1", server.Port);
        await accept;

        await server.SendBytesAsync([Iac, Do, Naws]);
        // Let the client process DO NAWS before reporting; a report sent earlier is dropped (RFC 1073).
        await DrainAsync(server, TimeSpan.FromMilliseconds(300));

        await connection.SendNawsAsync(120, 40);
        var report = await ReadUntilAsync(server, [Iac, Sb, Naws], TimeSpan.FromSeconds(3), requireEnd: true);
        await Assert.That(ContainsSequence(report, [Iac, Sb, Naws, 0, 120, 0, 40, Iac, Se])).IsTrue();

        // 70000 does not fit RFC 1073's unsigned 16-bit field; TNC throws, so we clamp to 65535,
        // whose 0xFF bytes are doubled on the wire.
        await connection.SendNawsAsync(70_000, 24);
        report = await ReadUntilAsync(server, [Iac, Sb, Naws], TimeSpan.FromSeconds(3), requireEnd: true);
        await Assert.That(ContainsSequence(report, [Iac, Sb, Naws, Iac, Iac, Iac, Iac, 0, 24, Iac, Se])).IsTrue();
    }

    private static async Task<string> ReadTtypeIsAsync(LoopbackServer server)
    {
        var bytes = await ReadUntilAsync(server, [Iac, Sb, Ttype, TtypeIs], TimeSpan.FromSeconds(3), requireEnd: true);
        var start = IndexOf(bytes, [Iac, Sb, Ttype, TtypeIs]) + 4;
        var end = IndexOf(bytes[start..], [Iac, Se]) + start;
        return Encoding.ASCII.GetString(bytes, start, end - start);
    }

    private static async Task<byte[]> ReadUntilAsync(
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

    private static async Task DrainAsync(LoopbackServer server, TimeSpan window)
    {
        var deadline = DateTime.UtcNow + window;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                await server.ReadAvailableBytesAsync(TimeSpan.FromMilliseconds(100));
            }
            catch (OperationCanceledException) { }
        }
    }

    private static bool ContainsSequence(byte[] haystack, byte[] needle) => IndexOf(haystack, needle) >= 0;

    private static int IndexOf(byte[] haystack, byte[] needle)
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
