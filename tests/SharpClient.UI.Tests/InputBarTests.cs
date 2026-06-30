using Bunit;
using Microsoft.AspNetCore.Components.Web;
using SharpClient.Core.Connection;
using SharpClient.Core.Presentation;
using SharpClient.Core.Sessions;
using SharpClient.UI.Components;

namespace SharpClient.UI.Tests;

public sealed class InputBarTests
{
    [Test]
    public async Task SendButtonDisabledWhenInputEmpty()
    {
        using var ctx = new BunitContext();
        var mgr = new SessionManager();
        var s = new UiFakeSession { State = ConnectionState.Connected };
        mgr.Add(s);
        var vm = new SessionsViewModel(mgr);

        var cut = ctx.Render<InputBar>(p => p.Add(c => c.Vm, vm));

        var btn = cut.Find("button");
        await Assert.That(btn.HasAttribute("disabled")).IsTrue();
    }

    [Test]
    public async Task SendButtonEnabledWhenInputNonEmpty()
    {
        using var ctx = new BunitContext();
        var mgr = new SessionManager();
        var s = new UiFakeSession { State = ConnectionState.Connected };
        mgr.Add(s);
        var vm = new SessionsViewModel(mgr) { Input = "look" };

        var cut = ctx.Render<InputBar>(p => p.Add(c => c.Vm, vm));

        var btn = cut.Find("button");
        await Assert.That(btn.HasAttribute("disabled")).IsFalse();
    }

    [Test]
    public async Task ClickSendDeliversCommandAndClearsInput()
    {
        using var ctx = new BunitContext();
        var mgr = new SessionManager();
        var s = new UiFakeSession { State = ConnectionState.Connected };
        mgr.Add(s);
        var vm = new SessionsViewModel(mgr) { Input = "look" };

        var cut = ctx.Render<InputBar>(p => p.Add(c => c.Vm, vm));

        await cut.Find("button").ClickAsync(new MouseEventArgs());

        await Assert.That(s.Sent).Contains("look");
        await Assert.That(vm.Input).IsEqualTo(string.Empty);
    }
}
