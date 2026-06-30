using SharpClient.Core.Sessions;

namespace SharpClient.Tests.Sessions;

public sealed class SessionManagerTests
{
    [Test]
    public async Task AddMakesSessionActiveAndFiresChanged()
    {
        var mgr = new SessionManager();
        var changed = 0;
        mgr.Changed += () => changed++;
        var a = new FakeSession();

        mgr.Add(a);

        await Assert.That(mgr.Sessions.Count).IsEqualTo(1);
        await Assert.That(mgr.Active).IsEqualTo(a);
        await Assert.That(changed).IsEqualTo(1);
    }

    [Test]
    public async Task ActivateSwitchesActive()
    {
        var mgr = new SessionManager();
        var a = new FakeSession();
        var b = new FakeSession();
        mgr.Add(a);
        mgr.Add(b);

        mgr.Activate(a);

        await Assert.That(mgr.Active).IsEqualTo(a);
    }

    [Test]
    public async Task CloseActiveDisposesAndMovesActiveToFirstRemaining()
    {
        var mgr = new SessionManager();
        var a = new FakeSession();
        var b = new FakeSession();
        mgr.Add(a);
        mgr.Add(b);
        mgr.Activate(b);

        await mgr.CloseAsync(b);

        await Assert.That(b.Disposed).IsTrue();
        await Assert.That(mgr.Sessions.Count).IsEqualTo(1);
        await Assert.That(mgr.Active).IsEqualTo(a);
    }

    [Test]
    public async Task CloseLastSessionLeavesNoActive()
    {
        var mgr = new SessionManager();
        var a = new FakeSession();
        mgr.Add(a);

        await mgr.CloseAsync(a);

        await Assert.That(mgr.Sessions.Count).IsEqualTo(0);
        await Assert.That(mgr.Active).IsNull();
    }

    // ── New tests ────────────────────────────────────────────────────────────

    [Test]
    public async Task CloseNonActiveSessionKeepsCurrentActive()
    {
        var mgr = new SessionManager();
        var a = new FakeSession();
        var b = new FakeSession();
        mgr.Add(a);
        mgr.Add(b);
        mgr.Activate(a); // a is active; b is not

        var changed = 0;
        mgr.Changed += () => changed++;

        await mgr.CloseAsync(b); // close non-active

        await Assert.That(mgr.Active).IsEqualTo(a);
        await Assert.That(mgr.Sessions.Count).IsEqualTo(1);
        // Close fires one Changed; b is disposed
        await Assert.That(changed).IsEqualTo(1);
        await Assert.That(b.Disposed).IsTrue();
    }

    [Test]
    public async Task ActivateUntrackedSessionIsNoOp()
    {
        var mgr = new SessionManager();
        var a = new FakeSession();
        mgr.Add(a);

        var changed = 0;
        mgr.Changed += () => changed++;

        var stranger = new FakeSession(); // never added
        mgr.Activate(stranger);

        // Active stays on a; Changed is not fired
        await Assert.That(mgr.Active).IsEqualTo(a);
        await Assert.That(changed).IsEqualTo(0);
    }

    [Test]
    public async Task CloseUntrackedSessionIsNoOp()
    {
        var mgr = new SessionManager();
        var a = new FakeSession();
        mgr.Add(a);

        var changed = 0;
        mgr.Changed += () => changed++;

        var stranger = new FakeSession(); // never added
        await mgr.CloseAsync(stranger);

        // Manager is unchanged; Changed is not fired
        await Assert.That(mgr.Active).IsEqualTo(a);
        await Assert.That(mgr.Sessions.Count).IsEqualTo(1);
        await Assert.That(changed).IsEqualTo(0);
        await Assert.That(stranger.Disposed).IsFalse();
    }

    [Test]
    public async Task CloseLastSessionDisposesAndFiresChangedOnce()
    {
        var mgr = new SessionManager();
        var a = new FakeSession();
        mgr.Add(a);

        var changed = 0;
        mgr.Changed += () => changed++;

        await mgr.CloseAsync(a);

        await Assert.That(mgr.Active).IsNull();
        await Assert.That(a.Disposed).IsTrue();
        // Add fires once (before the hook), Close fires once — only Close is counted here
        await Assert.That(changed).IsEqualTo(1);
    }
}
