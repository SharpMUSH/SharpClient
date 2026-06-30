using Microsoft.Extensions.DependencyInjection;
using SharpClient.Core.Connection;
using TelnetNegotiationCore.Builders;

namespace SharpClient.Tests.Connection;

public sealed class TelnetConnectionTests
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
        // Exactly one CR LF terminator — TNC adds it, we must not double it (regression guard).
        await Assert.That(sent.Contains("look\r\n")).IsTrue();
        await Assert.That(sent.Contains("look\r\n\r\n")).IsFalse();
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

    private static ReconnectOptions FastReconnect(int maxAttempts) => new()
    {
        InitialDelay = TimeSpan.FromMilliseconds(20),
        MaxDelay = TimeSpan.FromMilliseconds(40),
        MaxAttempts = maxAttempts,
    };

    [Test]
    public async Task DroppedConnectionTransitionsToReconnecting()
    {
        var server = new LoopbackServer();
        await using var connection = new TelnetConnection(CreateFactory(), FastReconnect(maxAttempts: 2));

        var reconnecting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.StateChanged += s =>
        {
            if (s == ConnectionState.Reconnecting)
            {
                reconnecting.TrySetResult();
            }
        };

        var accept = server.AcceptAsync();
        await connection.ConnectAsync("127.0.0.1", server.Port);
        await accept;
        await Assert.That(connection.State).IsEqualTo(ConnectionState.Connected);

        // Server drops the client: the read loop completes and the state must NOT
        // stay stuck on Connected — it should move to Reconnecting.
        await server.DisposeAsync();

        await reconnecting.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Test]
    public async Task DroppedConnectionEntersErrorAfterRetriesExhausted()
    {
        var server = new LoopbackServer();
        await using var connection = new TelnetConnection(CreateFactory(), FastReconnect(maxAttempts: 2));

        var error = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.StateChanged += s =>
        {
            if (s == ConnectionState.Error)
            {
                error.TrySetResult();
            }
        };

        var accept = server.AcceptAsync();
        await connection.ConnectAsync("127.0.0.1", server.Port);
        await accept;

        // Drop and keep the port closed so every reconnect attempt fails.
        await server.DisposeAsync();

        await error.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(connection.State).IsEqualTo(ConnectionState.Error);
    }

    [Test]
    public async Task IntentionalDisconnectDoesNotReconnect()
    {
        await using var server = new LoopbackServer();
        await using var connection = new TelnetConnection(CreateFactory(), FastReconnect(maxAttempts: 3));

        var sawReconnecting = false;
        connection.StateChanged += s =>
        {
            if (s == ConnectionState.Reconnecting)
            {
                sawReconnecting = true;
            }
        };

        var accept = server.AcceptAsync();
        await connection.ConnectAsync("127.0.0.1", server.Port);
        await accept;

        await connection.DisconnectAsync();

        // Give any erroneous reconnect loop time to fire.
        await Task.Delay(150);

        await Assert.That(connection.State).IsEqualTo(ConnectionState.Disconnected);
        await Assert.That(sawReconnecting).IsFalse();
    }
}
