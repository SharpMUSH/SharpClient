using Bunit;
using SharpClient.Core.Connection;
using SharpClient.Core.Presentation;
using SharpClient.Core.Sessions;
using SharpClient.UI.Components;

namespace SharpClient.UI.Tests;

public sealed class ProtocolPanelTests
{
    // A fake session with preset protocol data for UI tests.
    private sealed class ProtocolFakeSession : ISession
    {
        public IReadOnlyList<ScrollbackLine> Scrollback => [];
        public event Action<ScrollbackLine>? LineAppended { add { } remove { } }
        public event Action<ConnectionState>? StateChanged { add { } remove { } }
        public event Action? ProtocolChanged { add { } remove { } }
        public ConnectionState State => ConnectionState.Connected;
        public string CharacterName => "Vesper";
        public string WorldName => "Sindome";

        public IReadOnlyList<NegotiationEvent> NegotiationLog =>
        [
            new NegotiationEvent("TTYPE", "VT100"),
            new NegotiationEvent("NAWS", "80x24"),
            new NegotiationEvent("MSSP", "NAME=TestMUD PLAYERS=42"),
        ];

        public IReadOnlyList<GmcpMessage> GmcpLog =>
        [
            new GmcpMessage("Char.Vitals", """{"hp":320,"mp":140}"""),
            new GmcpMessage("Room.Info", """{"name":"Gate Tunnel"}"""),
        ];

        public Task ConnectAsync(string host, int port, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SendAsync(string line) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private static ProtocolPanelViewModel BuildVm(ISession session)
    {
        var mgr = new SessionManager();
        mgr.Add(session);
        return new ProtocolPanelViewModel(mgr);
    }

    [Test]
    public async Task RendersNegotiationRows()
    {
        using var ctx = new BunitContext();
        var vm = BuildVm(new ProtocolFakeSession());

        var cut = ctx.Render<ProtocolPanel>(p => p.Add(c => c.Vm, vm));

        var rows = cut.FindAll(".sc-negotiation-row");
        await Assert.That(rows.Count).IsEqualTo(3);
    }

    [Test]
    public async Task NegotiationRowShowsKeyAndDetail()
    {
        using var ctx = new BunitContext();
        var vm = BuildVm(new ProtocolFakeSession());

        var cut = ctx.Render<ProtocolPanel>(p => p.Add(c => c.Vm, vm));

        var keys = cut.FindAll(".sc-negotiation-key");
        var details = cut.FindAll(".sc-negotiation-detail");
        await Assert.That(keys[0].TextContent).IsEqualTo("TTYPE");
        await Assert.That(details[0].TextContent).IsEqualTo("VT100");
    }

    [Test]
    public async Task RendersGmcpPackages()
    {
        using var ctx = new BunitContext();
        var vm = BuildVm(new ProtocolFakeSession());

        var cut = ctx.Render<ProtocolPanel>(p => p.Add(c => c.Vm, vm));

        var rows = cut.FindAll(".sc-gmcp-row");
        await Assert.That(rows.Count).IsEqualTo(2);
    }

    [Test]
    public async Task GmcpRowShowsPackageAndJson()
    {
        using var ctx = new BunitContext();
        var vm = BuildVm(new ProtocolFakeSession());

        var cut = ctx.Render<ProtocolPanel>(p => p.Add(c => c.Vm, vm));

        var packages = cut.FindAll(".sc-gmcp-package");
        var jsons = cut.FindAll(".sc-gmcp-json");
        await Assert.That(packages[0].TextContent).IsEqualTo("Char.Vitals");
        await Assert.That(jsons[0].TextContent).Contains("hp");
    }

    [Test]
    public async Task EmptyLogsShowPlaceholders()
    {
        using var ctx = new BunitContext();
        var emptySession = new UiFakeSession();
        var mgr = new SessionManager();
        mgr.Add(emptySession);
        var vm = new ProtocolPanelViewModel(mgr);

        var cut = ctx.Render<ProtocolPanel>(p => p.Add(c => c.Vm, vm));

        var empties = cut.FindAll(".sc-protocol-empty");
        await Assert.That(empties.Count).IsEqualTo(2);
    }

    [Test]
    public async Task NullActiveSessionShowsPlaceholders()
    {
        using var ctx = new BunitContext();
        var mgr = new SessionManager(); // no sessions — Active is null
        var vm = new ProtocolPanelViewModel(mgr);

        var cut = ctx.Render<ProtocolPanel>(p => p.Add(c => c.Vm, vm));

        var empties = cut.FindAll(".sc-protocol-empty");
        await Assert.That(empties.Count).IsEqualTo(2);
    }
}
