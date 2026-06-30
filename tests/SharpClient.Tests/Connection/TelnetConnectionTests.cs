using Microsoft.Extensions.DependencyInjection;
using SharpClient.Core.Connection;
using TelnetNegotiationCore.Builders;

namespace SharpClient.Tests.Connection;

public sealed class TelnetConnectionTests
{
    private static ITelnetInterpreterFactory CreateFactory()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTelnetClient();
        return services.BuildServiceProvider().GetRequiredService<ITelnetInterpreterFactory>();
    }

    [Test]
    public async Task ReceivesLineFromServer()
    {
        await using var server = new LoopbackServer();
        await using var connection = new TelnetConnection(CreateFactory());

        var received = new TaskCompletionSource<string>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        connection.LineReceived += line => received.TrySetResult(line);

        var accept = server.AcceptAsync();
        await connection.ConnectAsync("127.0.0.1", server.Port);
        await accept;

        await server.SendLineAsync("Hello, MUSH");

        var line = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(line).IsEqualTo("Hello, MUSH");
    }

    [Test]
    public async Task SendDeliversBytesToServer()
    {
        await using var server = new LoopbackServer();
        await using var connection = new TelnetConnection(CreateFactory());

        var accept = server.AcceptAsync();
        await connection.ConnectAsync("127.0.0.1", server.Port);
        await accept;

        await connection.SendAsync("look");

        var sent = await server.ReadAvailableAsync(TimeSpan.FromSeconds(5));
        await Assert.That(sent.Contains("look")).IsTrue();
    }

    [Test]
    public async Task StateBecomesConnectedAfterConnect()
    {
        await using var server = new LoopbackServer();
        await using var connection = new TelnetConnection(CreateFactory());

        var accept = server.AcceptAsync();
        await connection.ConnectAsync("127.0.0.1", server.Port);
        await accept;

        await Assert.That(connection.State).IsEqualTo(ConnectionState.Connected);
    }
}
