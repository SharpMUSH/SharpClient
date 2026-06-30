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

    [Test]
    public async Task ClosedSessionHistoryIsPruned()
    {
        var mgr = new SessionManager();
        var vm = new SessionsViewModel(mgr);
        var session = new FakeSession { State = ConnectionState.Connected };
        mgr.Add(session);
        vm.Input = "look";
        await vm.SendAsync();

        await Assert.That(vm.TrackedHistoryCount).IsEqualTo(1);

        await mgr.CloseAsync(session);

        await Assert.That(vm.TrackedHistoryCount).IsEqualTo(0);
    }

    [Test]
    public async Task OpenSessionHistoryRetainedWhenOtherSessionClosed()
    {
        var mgr = new SessionManager();
        var vm = new SessionsViewModel(mgr);
        var a = new FakeSession { State = ConnectionState.Connected };
        var b = new FakeSession { State = ConnectionState.Connected };
        mgr.Add(a);
        mgr.Add(b);

        vm.Select(a);
        vm.Input = "north";
        await vm.SendAsync();

        vm.Select(b);
        vm.Input = "look";
        await vm.SendAsync();

        await mgr.CloseAsync(a);

        vm.Select(b);
        await Assert.That(vm.History).Contains("look");
    }

    [Test]
    public async Task LineAppendedOnActiveSessionRaisesVmChanged()
    {
        var mgr = new SessionManager();
        var session = new FakeSession { State = ConnectionState.Connected };
        mgr.Add(session); // makes session active; fires manager.Changed
        var vm = new SessionsViewModel(mgr); // constructor subscribes to LineAppended

        var fired = false;
        vm.Changed += () => fired = true;

        // Simulate server line arriving on the active session.
        session.Append(new ScrollbackLine([]));

        await Assert.That(fired).IsTrue();
    }
}
