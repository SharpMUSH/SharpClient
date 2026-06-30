using Microsoft.Extensions.DependencyInjection;
using SharpClient.Core.Connection;
using SharpClient.Core.Sessions;
using SharpClient.Tests.Connection;
using TelnetNegotiationCore.Builders;

namespace SharpClient.Tests.Sessions;

public sealed class SessionTests
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
    public async Task ReceivedLineIsParsedIntoScrollback()
    {
        await using var server = new LoopbackServer();
        await using var session = new Session(new TelnetConnection(CreateFactory()));

        var appended = new TaskCompletionSource<ScrollbackLine>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        session.LineAppended += line => appended.TrySetResult(line);

        var accept = server.AcceptAsync();
        await session.ConnectAsync("127.0.0.1", server.Port);
        await accept;

        await server.SendLineAsync("\e[31mAlert\e[0m");

        var line = await appended.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(line.Segments.Count).IsEqualTo(1);
        await Assert.That(line.Segments[0].Text).IsEqualTo("Alert");
        await Assert.That(session.Scrollback.Count).IsEqualTo(1);
    }
}
