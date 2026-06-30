using SharpClient.Core.Connection;
using SharpClient.Core.Diagnostics;
using SharpClient.Core.Persistence;
using SharpClient.Core.Platform;
using SharpClient.Core.Presentation;
using SharpClient.Core.Sessions;
using SharpClient.Core.Triggers;
using SharpClient.Data;
using SharpClient.Web;
using SharpClient.Web.Components;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// ── TNC telnet runtime ─────────────────────────────────────────────────────
builder.Services.AddTelnetClient();
builder.Services.AddSingleton<ITelnetConnectionFactory, TelnetConnectionFactory>();

// ── Platform services ──────────────────────────────────────────────────────
builder.Services.AddSingleton<IPreferences, WebPreferences>();
builder.Services.AddSingleton<SettingsViewModel>(sp =>
    new SettingsViewModel(sp.GetRequiredService<IPreferences>()));

builder.Services.AddSingleton<IAppStorage, WebAppStorage>();
builder.Services.AddSingleton<ISecretStore, WebSecretStore>();

// ── Trigger / alias engines (stateless singletons) ─────────────────────────
builder.Services.AddSingleton<ITriggerEngine, TriggerEngine>();
builder.Services.AddSingleton<IAliasEngine, AliasEngine>();

// ── Platform notifier ──────────────────────────────────────────────────────
builder.Services.AddSingleton<INotifier, WebNotifier>();

// ── Diagnostics ─────────────────────────────────────────────────────────────
// The Web preview has no persistent file log; the no-op exporter keeps SettingsView's
// ILogExporter injection satisfiable and hides the export affordance (IsAvailable == false).
builder.Services.AddSingleton<ILogExporter, NoopLogExporter>();

// ── Session management ─────────────────────────────────────────────────────
builder.Services.AddSingleton<SessionManager>();
builder.Services.AddSingleton<ISessionManager>(sp => sp.GetRequiredService<SessionManager>());
builder.Services.AddSingleton<SessionsViewModel>(sp =>
    new SessionsViewModel(sp.GetRequiredService<ISessionManager>()));
builder.Services.AddSingleton<ProtocolPanelViewModel>(sp =>
    new ProtocolPanelViewModel(sp.GetRequiredService<ISessionManager>()));

// ── Data / persistence ─────────────────────────────────────────────────────
builder.Services.AddScoped<AppDbContext>();
builder.Services.AddScoped<IWorldStore, WorldStore>();
builder.Services.AddScoped<ISessionHistory, SessionHistory>();

// ── Session launcher (real telnet) ─────────────────────────────────────────
builder.Services.AddScoped<ISessionLauncher, TelnetSessionLauncher>();

// ── View models ────────────────────────────────────────────────────────────
builder.Services.AddScoped<WorldManagerViewModel>(sp => new WorldManagerViewModel(
    sp.GetRequiredService<IWorldStore>(),
    sp.GetRequiredService<ISecretStore>(),
    sp.GetRequiredService<ISessionManager>(),
    sp.GetRequiredService<ISessionLauncher>()));
builder.Services.AddScoped<TriggerAliasEditorViewModel>(sp =>
    new TriggerAliasEditorViewModel(
        sp.GetRequiredService<IWorldStore>()));
builder.Services.AddScoped<HistorySearchViewModel>(sp =>
    new HistorySearchViewModel(
        sp.GetRequiredService<ISessionHistory>(),
        sp.GetRequiredService<IWorldStore>()));

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(typeof(SharpClient.UI.Components.SessionScreen).Assembly);

app.Run();
