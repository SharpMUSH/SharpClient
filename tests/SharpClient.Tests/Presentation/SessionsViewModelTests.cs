using SharpClient.Core.Connection;
using SharpClient.Core.Presentation;
using SharpClient.Core.Sessions;
using SharpClient.Tests.Sessions;

namespace SharpClient.Tests.Presentation;

public sealed class SessionsViewModelTests
{
    private static readonly string[] NorthArray = ["north"];
    private static readonly string[] LookArray = ["look"];
    private static readonly string[] LookNorthArray = ["look", "north"];

    [Test]
    public async Task CanSendOnlyWhenConnectedAndInputNonEmpty()
    {
        var mgr = new SessionManager();
        var s = new FakeSession { State = ConnectionState.Connected };
        mgr.Add(s);
        var vm = new SessionsViewModel(mgr);

        await Assert.That(vm.CanSend).IsFalse();
        vm.Input = "look";
        await Assert.That(vm.CanSend).IsTrue();

        s.State = ConnectionState.Error;
        await Assert.That(vm.CanSend).IsFalse();
    }

    [Test]
    public async Task SendDeliversToActiveAndRecordsHistory()
    {
        var mgr = new SessionManager();
        var s = new FakeSession { State = ConnectionState.Connected };
        mgr.Add(s);
        var vm = new SessionsViewModel(mgr) { Input = "north" };

        await vm.SendAsync();

        await Assert.That(s.Sent).IsEquivalentTo(NorthArray);
        await Assert.That(vm.Input).IsEqualTo(string.Empty);
        await Assert.That(vm.History).IsEquivalentTo(NorthArray);
    }

    [Test]
    public async Task HistoryIsMostRecentFirstAndDeduped()
    {
        var mgr = new SessionManager();
        var s = new FakeSession { State = ConnectionState.Connected };
        mgr.Add(s);
        var vm = new SessionsViewModel(mgr);

        vm.Input = "look"; await vm.SendAsync();
        vm.Input = "north"; await vm.SendAsync();
        vm.Input = "look"; await vm.SendAsync();

        await Assert.That(vm.History).IsEquivalentTo(LookNorthArray);
    }

    [Test]
    public async Task SelectActivatesSessionInManager()
    {
        var mgr = new SessionManager();
        var a = new FakeSession();
        var b = new FakeSession();
        mgr.Add(a);
        mgr.Add(b);
        var vm = new SessionsViewModel(mgr);

        vm.Select(a);

        await Assert.That(vm.Active).IsEqualTo(a);
    }
}
