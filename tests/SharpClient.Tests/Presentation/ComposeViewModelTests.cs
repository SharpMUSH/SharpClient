using SharpClient.Core.Connection;
using SharpClient.Core.Formatting;
using SharpClient.Core.Presentation;
using SharpClient.Core.Sessions;
using SharpClient.Tests.Fakes;
using SharpClient.Tests.Sessions;

namespace SharpClient.Tests.Presentation;

public sealed class ComposeViewModelTests
{
    private static (ComposeViewModel vm, SessionManager mgr, FakePreferences prefs) Build()
    {
        var mgr = new SessionManager();
        var prefs = new FakePreferences();
        return (new ComposeViewModel(mgr, prefs), mgr, prefs);
    }

    [Test]
    public async Task CannotSendWithNoSession()
    {
        var (vm, _, _) = Build();
        await Assert.That(vm.CanSend).IsFalse();
    }

    [Test]
    public async Task CannotSendWhenDisconnected()
    {
        var (vm, mgr, _) = Build();
        var s = new FakeSession { State = ConnectionState.Disconnected };
        mgr.Add(s);
        vm.Body = "waves";

        await Assert.That(vm.CanSend).IsFalse();
    }

    [Test]
    public async Task CannotSendWhenBodyIsBlank()
    {
        var (vm, mgr, _) = Build();
        mgr.Add(new FakeSession { State = ConnectionState.Connected });
        vm.Body = "   ";

        await Assert.That(vm.CanSend).IsFalse();
    }

    [Test]
    public async Task CannotSendWhenCustomPrefixIsBlank()
    {
        var (vm, mgr, _) = Build();
        mgr.Add(new FakeSession { State = ConnectionState.Connected });
        vm.Body = "waves";
        vm.SelectedPrefix = PosePrefix.Custom;
        vm.CustomPrefix = "  ";

        await Assert.That(vm.CanSend).IsFalse();
    }

    [Test]
    public async Task CanSendWhenConnectedWithBody()
    {
        var (vm, mgr, _) = Build();
        mgr.Add(new FakeSession { State = ConnectionState.Connected });
        vm.Body = "waves";

        await Assert.That(vm.CanSend).IsTrue();
    }

    [Test]
    public async Task PreviewUsesSelectedPrefixAndEscapes()
    {
        var (vm, mgr, _) = Build();
        mgr.Add(new FakeSession { State = ConnectionState.Connected });
        vm.SelectedPrefix = PosePrefix.Emit;
        vm.Body = "50% off\nsecond line";

        await Assert.That(vm.Preview).IsEqualTo("@emit 50%% off%rsecond line");
    }

    [Test]
    public async Task SendDeliversFormattedLineAndClearsDraft()
    {
        var (vm, mgr, _) = Build();
        var s = new FakeSession { State = ConnectionState.Connected };
        mgr.Add(s);
        vm.Body = "grins\nwidely";

        await vm.SendAsync();

        await Assert.That(s.Sent).Contains("pose grins%rwidely");
        await Assert.That(vm.Body).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task SendDoesNothingWhenCannotSend()
    {
        var (vm, mgr, _) = Build();
        var s = new FakeSession { State = ConnectionState.Disconnected };
        mgr.Add(s);
        vm.Body = "grins";

        await vm.SendAsync();

        await Assert.That(s.Sent).IsEmpty();
    }

    [Test]
    public async Task DraftsAreKeptPerSession()
    {
        var (vm, mgr, _) = Build();
        var a = new FakeSession { State = ConnectionState.Connected };
        var b = new FakeSession { State = ConnectionState.Connected };
        mgr.Add(a);
        vm.Body = "draft for a";
        mgr.Add(b);
        vm.Body = "draft for b";

        mgr.Activate(a);
        await Assert.That(vm.Body).IsEqualTo("draft for a");

        mgr.Activate(b);
        await Assert.That(vm.Body).IsEqualTo("draft for b");
    }

    [Test]
    public async Task DraftsArePrunedWhenSessionCloses()
    {
        var (vm, mgr, _) = Build();
        var a = new FakeSession { State = ConnectionState.Connected };
        mgr.Add(a);
        vm.Body = "draft for a";

        await mgr.CloseAsync(a);

        await Assert.That(vm.TrackedDraftCount).IsEqualTo(0);
    }

    [Test]
    public async Task CustomPrefixPersistsPerWorld()
    {
        var worldA = Guid.NewGuid();
        var worldB = Guid.NewGuid();
        var (vm, mgr, prefs) = Build();
        var a = new FakeSession { State = ConnectionState.Connected, WorldId = worldA };
        var b = new FakeSession { State = ConnectionState.Connected, WorldId = worldB };

        mgr.Add(a);
        vm.SelectedPrefix = PosePrefix.Custom;
        vm.CustomPrefix = "page Alice=";

        mgr.Add(b);
        vm.CustomPrefix = "page Bob=";

        await Assert.That(prefs.GetString($"compose.custom.{worldA}", "")).IsEqualTo("page Alice=");
        await Assert.That(prefs.GetString($"compose.custom.{worldB}", "")).IsEqualTo("page Bob=");
    }

    [Test]
    public async Task CustomPrefixIsReloadedWhenActiveSessionChanges()
    {
        var worldA = Guid.NewGuid();
        var worldB = Guid.NewGuid();
        var (vm, mgr, _) = Build();
        var a = new FakeSession { State = ConnectionState.Connected, WorldId = worldA };
        var b = new FakeSession { State = ConnectionState.Connected, WorldId = worldB };

        mgr.Add(a);
        vm.CustomPrefix = "page Alice=";
        mgr.Add(b);
        vm.CustomPrefix = "page Bob=";

        mgr.Activate(a);

        await Assert.That(vm.CustomPrefix).IsEqualTo("page Alice=");
    }

    [Test]
    public async Task ChangedFiresWhenConnectionStateChanges()
    {
        var (vm, mgr, _) = Build();
        var s = new FakeSession { State = ConnectionState.Disconnected };
        mgr.Add(s);
        var fired = 0;
        vm.Changed += () => fired++;

        s.RaiseState(ConnectionState.Connected);

        await Assert.That(fired).IsEqualTo(1);
        await Assert.That(vm.CanSend).IsFalse();
    }

    [Test]
    public async Task ClearEmptiesOnlyTheActiveDraft()
    {
        var (vm, mgr, _) = Build();
        var a = new FakeSession { State = ConnectionState.Connected };
        var b = new FakeSession { State = ConnectionState.Connected };
        mgr.Add(a);
        vm.Body = "keep me";
        mgr.Add(b);
        vm.Body = "drop me";

        vm.Clear();

        await Assert.That(vm.Body).IsEqualTo(string.Empty);
        mgr.Activate(a);
        await Assert.That(vm.Body).IsEqualTo("keep me");
    }
}
