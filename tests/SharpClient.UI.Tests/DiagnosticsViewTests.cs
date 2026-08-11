using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using SharpClient.Core.Diagnostics;
using SharpClient.UI.Components;

namespace SharpClient.UI.Tests;

public sealed class DiagnosticsViewTests
{
    private static readonly DateTimeOffset Base = new(2026, 8, 10, 23, 41, 0, TimeSpan.Zero);

    private static (BunitContext ctx, UiFakeLogReader reader) NewContext()
    {
        var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        var reader = new UiFakeLogReader();
        ctx.Services.AddSingleton<ILogReader>(reader);
        ctx.Services.AddSingleton<ILogExporter>(new NoopLogExporter());
        return (ctx, reader);
    }

    private static void Seed(UiFakeLogReader reader)
    {
        reader.Entries.Add(new LogEntry(Base.AddSeconds(2), "CRASH", "AppDomain", "boom", "stack frame here"));
        reader.Entries.Add(new LogEntry(Base.AddSeconds(1), "Error", "Session", "send failed", null));
        reader.Entries.Add(new LogEntry(Base, "Information", "App", "started", null));
    }

    [Test]
    public async Task RendersEveryEntryByDefault()
    {
        var (ctx, reader) = NewContext();
        using var _ = ctx;
        Seed(reader);

        var cut = ctx.Render<DiagnosticsView>();

        await Assert.That(cut.FindAll(".sc-diag-entry")).Count().IsEqualTo(3);
    }

    [Test]
    public async Task CrashesFilterShowsOnlyCrashEntries()
    {
        var (ctx, reader) = NewContext();
        using var _ = ctx;
        Seed(reader);

        var cut = ctx.Render<DiagnosticsView>();
        await cut.FindAll(".sc-diag-chip")[2].ClickAsync(new MouseEventArgs());

        await Assert.That(cut.FindAll(".sc-diag-entry")).Count().IsEqualTo(1);
        await Assert.That(cut.Find(".sc-diag-entry").TextContent).Contains("boom");
    }

    [Test]
    public async Task ErrorsFilterIncludesErrorsAndCrashes()
    {
        var (ctx, reader) = NewContext();
        using var _ = ctx;
        Seed(reader);

        var cut = ctx.Render<DiagnosticsView>();
        await cut.FindAll(".sc-diag-chip")[1].ClickAsync(new MouseEventArgs());

        await Assert.That(cut.FindAll(".sc-diag-entry")).Count().IsEqualTo(2);
    }

    [Test]
    public async Task InitialFilterParameterSelectsCrashes()
    {
        var (ctx, reader) = NewContext();
        using var _ = ctx;
        Seed(reader);

        var cut = ctx.Render<DiagnosticsView>(p => p.Add(c => c.InitialFilter, "crashes"));

        await Assert.That(cut.FindAll(".sc-diag-entry")).Count().IsEqualTo(1);
    }

    [Test]
    public async Task DetailIsRenderedForEntriesThatHaveIt()
    {
        var (ctx, reader) = NewContext();
        using var _ = ctx;
        Seed(reader);

        var cut = ctx.Render<DiagnosticsView>();

        await Assert.That(cut.FindAll("details")).Count().IsEqualTo(1);
        await Assert.That(cut.Find("details").TextContent).Contains("stack frame here");
    }

    [Test]
    public async Task EmptyStateIsShownWhenThereAreNoEntries()
    {
        var (ctx, _) = NewContext();
        using var __ = ctx;

        var cut = ctx.Render<DiagnosticsView>();

        await Assert.That(cut.FindAll(".sc-diag-empty")).Count().IsEqualTo(1);
    }

    [Test]
    public async Task ClearRequiresASecondConfirmingClick()
    {
        var (ctx, reader) = NewContext();
        using var _ = ctx;
        Seed(reader);

        var cut = ctx.Render<DiagnosticsView>();
        await cut.Find(".sc-diag-clear").ClickAsync(new MouseEventArgs());

        await Assert.That(reader.ClearCalls).IsEqualTo(0);
        await Assert.That(cut.Find(".sc-diag-clear").TextContent.Trim()).IsEqualTo("Confirm clear");

        await cut.Find(".sc-diag-clear").ClickAsync(new MouseEventArgs());

        await Assert.That(reader.ClearCalls).IsEqualTo(1);
        await Assert.That(cut.FindAll(".sc-diag-entry")).IsEmpty();
    }
}
