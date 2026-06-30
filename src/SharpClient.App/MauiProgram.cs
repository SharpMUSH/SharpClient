using Microsoft.Extensions.Logging;
using SharpClient.App.Services;
using SharpClient.Core.Connection;
using SharpClient.Core.Platform;
using SharpClient.Core.Persistence;
using SharpClient.Core.Presentation;
using SharpClient.Core.Sessions;
using SharpClient.Core.Triggers;
using SharpClient.Data;

namespace SharpClient.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        builder.Services.AddMauiBlazorWebView();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        // ── TNC telnet runtime ─────────────────────────────────────────────
        // AddTelnetClient() is an extension method from TelnetNegotiationCore and
        // registers ITelnetInterpreterFactory. The TNC package IS referenced in
        // App.csproj so AddTelnetClient() resolves, but direct use of
        // ITelnetInterpreterFactory in App C# code fails under net10.0-android
        // (Stateless version mismatch prevents Roslyn from loading TNC metadata).
        // The TelnetConnectionFactory wrapper in Core bridges the gap.
        builder.Services.AddTelnetClient();
        builder.Services.AddSingleton<ITelnetConnectionFactory, TelnetConnectionFactory>();

        // ── Platform services ─────────────────────────────────────────────
        builder.Services.AddSingleton<IAppStorage, MauiAppStorage>();
        builder.Services.AddSingleton<ISecretStore, MauiSecretStore>();
        builder.Services.AddSingleton<INotifier, MauiNotifier>();

        // ── Data / persistence ────────────────────────────────────────────
        // AppDbContext is transient so each call gets a fresh context; this
        // avoids cross-thread SQLite issues and mirrors the Web's per-request
        // scoped pattern without requiring HTTP request scopes in MAUI.
        builder.Services.AddTransient<AppDbContext>();
        builder.Services.AddTransient<IWorldStore, WorldStore>();
        builder.Services.AddTransient<ISessionHistory, SessionHistory>();

        // ── Session management ────────────────────────────────────────────
        builder.Services.AddSingleton<SessionManager>();
        builder.Services.AddSingleton<ISessionManager>(sp =>
            sp.GetRequiredService<SessionManager>());

        // ── View models ───────────────────────────────────────────────────
        builder.Services.AddSingleton<SessionsViewModel>(sp =>
            new SessionsViewModel(sp.GetRequiredService<ISessionManager>()));

        builder.Services.AddSingleton<ProtocolPanelViewModel>(sp =>
            new ProtocolPanelViewModel(sp.GetRequiredService<ISessionManager>()));

        builder.Services.AddTransient<WorldManagerViewModel>(sp =>
            new WorldManagerViewModel(
                sp.GetRequiredService<IWorldStore>(),
                sp.GetRequiredService<ISecretStore>(),
                sp.GetRequiredService<ISessionManager>(),
                sp.GetRequiredService<ISessionLauncher>()));

        // ── Session launcher (real telnet) ────────────────────────────────
        builder.Services.AddTransient<ISessionLauncher, TelnetSessionLauncher>();

        // ── Trigger / alias engines (stateless) ──────────────────────────
        builder.Services.AddSingleton<ITriggerEngine, TriggerEngine>();
        builder.Services.AddSingleton<IAliasEngine, AliasEngine>();

        return builder.Build();
    }
}
