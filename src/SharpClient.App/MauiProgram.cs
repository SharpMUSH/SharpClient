using Microsoft.Extensions.Logging;
using Plugin.LocalNotification;
using Plugin.LocalNotification.Core.Models.AndroidOption;
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
            })
            .UseLocalNotification(config =>
            {
                config.AddAndroid(android =>
                    android.AddChannel(new AndroidNotificationChannelRequest
                    {
                        Id = MauiNotifier.AlertChannelId,
                        Name = "Alerts",
                        Description = "Trigger and activity alerts from MUSH sessions.",
                        Importance = AndroidImportance.High,
                    }));
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
#if ANDROID
        builder.Services.AddSingleton<IConnectionKeepAlive, AndroidConnectionKeepAlive>();
#else
        builder.Services.AddSingleton<IConnectionKeepAlive, NoopConnectionKeepAlive>();
#endif
        builder.Services.AddSingleton<ConnectionKeepAliveCoordinator>();

        builder.Services.AddSingleton<IAppStorage, MauiAppStorage>();
        builder.Services.AddSingleton<ISecretStore, MauiSecretStore>();
        builder.Services.AddSingleton<INotifier, MauiNotifier>();
        builder.Services.AddSingleton<SharpClient.Core.Platform.IPreferences, MauiPreferences>();
        builder.Services.AddSingleton<SettingsViewModel>(sp =>
            new SettingsViewModel(sp.GetRequiredService<SharpClient.Core.Platform.IPreferences>()));

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
        builder.Services.AddTransient<ISessionLauncher, SharpClient.Core.Sessions.TelnetSessionLauncher>();

        // ── Trigger / alias engines (stateless) ──────────────────────────
        builder.Services.AddSingleton<ITriggerEngine, TriggerEngine>();
        builder.Services.AddSingleton<IAliasEngine, AliasEngine>();

        var app = builder.Build();

        // Eagerly resolve the coordinator so it begins observing session/connection activity at
        // startup (DI singletons are otherwise lazily constructed on first request).
        app.Services.GetRequiredService<ConnectionKeepAliveCoordinator>();

        return app;
    }
}
