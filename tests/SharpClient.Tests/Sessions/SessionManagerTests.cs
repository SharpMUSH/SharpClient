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
}
