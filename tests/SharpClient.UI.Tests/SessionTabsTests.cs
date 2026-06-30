using Bunit;
using SharpClient.Core.Connection;
using SharpClient.Core.Presentation;
using SharpClient.Core.Sessions;
using SharpClient.UI.Components;

namespace SharpClient.UI.Tests;

public sealed class SessionTabsTests
{
    [Test]
    public async Task RendersOneTabPerSession()
    {
        using var ctx = new BunitContext();
        var mgr = new SessionManager();
        var s1 = new UiFakeSession { CharacterName = "Vesper", WorldName = "Sindome", State = ConnectionState.Connected };
        var s2 = new UiFakeSession { CharacterName = "Thorne", WorldName = "GrapevineMUD", State = ConnectionState.Error };
        mgr.Add(s1);
        mgr.Add(s2);
        var vm = new SessionsViewModel(mgr);

        var cut = ctx.Render<SessionTabs>(p => p.Add(c => c.Vm, vm));

        var tabs = cut.FindAll(".sc-tab");
        await Assert.That(tabs.Count).IsEqualTo(2);
        await Assert.That(tabs[0].TextContent).Contains("Vesper");
        await Assert.That(tabs[1].TextContent).Contains("Thorne");
    }

    [Test]
    public async Task ErrorTabDotUsesRedColor()
    {
        using var ctx = new BunitContext();
        var mgr = new SessionManager();
        var s1 = new UiFakeSession { CharacterName = "Vesper", State = ConnectionState.Connected };
        var s2 = new UiFakeSession { CharacterName = "Thorne", State = ConnectionState.Error };
        mgr.Add(s1);
        mgr.Add(s2);
        var vm = new SessionsViewModel(mgr);

        var cut = ctx.Render<SessionTabs>(p => p.Add(c => c.Vm, vm));

        var dots = cut.FindAll(".sc-state-dot");
        await Assert.That(dots[1].GetAttribute("style")).Contains("#e06c75");
    }

    [Test]
    public async Task ActiveTabHasActiveClass()
    {
        using var ctx = new BunitContext();
        var mgr = new SessionManager();
        var s1 = new UiFakeSession { CharacterName = "Vesper", State = ConnectionState.Connected };
        var s2 = new UiFakeSession { CharacterName = "Thorne", State = ConnectionState.Error };
        mgr.Add(s1);
        mgr.Add(s2);
        var vm = new SessionsViewModel(mgr);
        vm.Select(s1);

        var cut = ctx.Render<SessionTabs>(p => p.Add(c => c.Vm, vm));

        var tabs = cut.FindAll(".sc-tab");
        await Assert.That(tabs[0].ClassList).Contains("sc-tab-active");
        await Assert.That(tabs[1].ClassList).DoesNotContain("sc-tab-active");
    }
}
