using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using SharpClient.Core.Diagnostics;
using SharpClient.UI.Components;

namespace SharpClient.UI.Tests;

public sealed class CrashBannerTests
{
    private static (BunitContext ctx, UiFakeLogReader reader) NewContext(CrashReport? pending)
    {
        var ctx = new BunitContext();
        var reader = new UiFakeLogReader { Pending = pending };
        ctx.Services.AddSingleton<ILogReader>(reader);
        return (ctx, reader);
    }

    [Test]
    public async Task NothingIsRenderedWhenThereIsNoPendingCrash()
    {
        var (ctx, _) = NewContext(null);
        using var _unused = ctx;

        var cut = ctx.Render<CrashBanner>();

        await Assert.That(cut.FindAll(".sc-crash-banner")).IsEmpty();
    }

    [Test]
    public async Task BannerShowsTheCrashTimestamp()
    {
        var report = new CrashReport(
            new DateTimeOffset(2026, 8, 9, 23, 41, 0, TimeSpan.Zero), "AppDomain", "boom", "stack");
        var (ctx, _) = NewContext(report);
        using var _unused = ctx;

        var cut = ctx.Render<CrashBanner>();

        await Assert.That(cut.FindAll(".sc-crash-banner")).Count().IsEqualTo(1);
        await Assert.That(cut.Find(".sc-crash-banner").TextContent).Contains("2026-08-09 23:41");
    }

    [Test]
    public async Task DismissingHidesTheBannerAndTellsTheReader()
    {
        var report = new CrashReport(
            new DateTimeOffset(2026, 8, 9, 23, 41, 0, TimeSpan.Zero), "AppDomain", "boom", "stack");
        var (ctx, reader) = NewContext(report);
        using var _unused = ctx;

        var cut = ctx.Render<CrashBanner>();
        await cut.Find(".sc-crash-dismiss").ClickAsync(new MouseEventArgs());

        await Assert.That(reader.DismissCalls).IsEqualTo(1);
        await Assert.That(cut.FindAll(".sc-crash-banner")).IsEmpty();
    }
}
