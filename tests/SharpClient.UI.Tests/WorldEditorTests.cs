using Bunit;
using SharpClient.Core.Presentation;
using SharpClient.Core.Sessions;
using SharpClient.UI.Components;

namespace SharpClient.UI.Tests;

public sealed class WorldEditorTests
{
    [Test]
    public async Task SelfSignedOptionAppearsOnlyWithTlsAndIsSaved()
    {
        var store = new UiFakeWorldStore();
        var vm = new WorldManagerViewModel(store, new UiFakeSecretStore(), new SessionManager(), new UiFakeSessionLauncher());
        using var ctx = new BunitContext();
        var cut = ctx.Render<WorldEditor>(p => p.Add(c => c.Vm, vm));

        cut.Find("input.sc-field-input").Input("Secure");
        cut.FindAll("input.sc-field-input")[1].Input("tls.example");
        await Assert.That(cut.FindAll(".sc-world-invalid-cert").Count).IsEqualTo(0);

        cut.Find(".sc-world-tls").Change(true);
        cut.Find(".sc-world-invalid-cert").Change(true);
        cut.Find(".sc-modal-submit").Click();

        var saved = (await store.GetWorldsAsync()).Single();
        await Assert.That(saved.UseTls).IsTrue();
        await Assert.That(saved.AllowInvalidCertificate).IsTrue();
    }

    [Test]
    public async Task TurningTlsOffDropsTheSelfSignedAllowance()
    {
        var store = new UiFakeWorldStore();
        var vm = new WorldManagerViewModel(store, new UiFakeSecretStore(), new SessionManager(), new UiFakeSessionLauncher());
        using var ctx = new BunitContext();
        var cut = ctx.Render<WorldEditor>(p => p.Add(c => c.Vm, vm));

        cut.Find("input.sc-field-input").Input("Plain");
        cut.FindAll("input.sc-field-input")[1].Input("plain.example");
        cut.Find(".sc-world-tls").Change(true);
        cut.Find(".sc-world-invalid-cert").Change(true);
        cut.Find(".sc-world-tls").Change(false);
        cut.Find(".sc-modal-submit").Click();

        var saved = (await store.GetWorldsAsync()).Single();
        await Assert.That(saved.UseTls).IsFalse();
        await Assert.That(saved.AllowInvalidCertificate).IsFalse();
    }
}
