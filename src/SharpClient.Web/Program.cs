using SharpClient.Core.Connection;
using SharpClient.Core.Platform;
using SharpClient.Core.Presentation;
using SharpClient.Core.Sessions;
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
builder.Services.AddSingleton<SharpClient.Core.Persistence.ISecretStore, WebSecretStore>();

// ── Session management ─────────────────────────────────────────────────────
builder.Services.AddSingleton<SessionManager>();
builder.Services.AddSingleton<ISessionManager>(sp => sp.GetRequiredService<SessionManager>());
builder.Services.AddSingleton<SessionsViewModel>(sp =>
    new SessionsViewModel(sp.GetRequiredService<ISessionManager>()));
builder.Services.AddSingleton<ProtocolPanelViewModel>(sp =>
    new ProtocolPanelViewModel(sp.GetRequiredService<ISessionManager>()));

// ── Data / persistence ─────────────────────────────────────────────────────
builder.Services.AddScoped<SharpClient.Data.AppDbContext>();
builder.Services.AddScoped<SharpClient.Core.Persistence.IWorldStore, SharpClient.Data.WorldStore>();

// ── Session launcher (real telnet) ─────────────────────────────────────────
builder.Services.AddScoped<ISessionLauncher, TelnetSessionLauncher>();

// ── View models ────────────────────────────────────────────────────────────
builder.Services.AddScoped<WorldManagerViewModel>(sp => new WorldManagerViewModel(
    sp.GetRequiredService<SharpClient.Core.Persistence.IWorldStore>(),
    sp.GetRequiredService<SharpClient.Core.Persistence.ISecretStore>(),
    sp.GetRequiredService<ISessionManager>(),
    sp.GetRequiredService<ISessionLauncher>()));
builder.Services.AddScoped<TriggerAliasEditorViewModel>(sp =>
    new TriggerAliasEditorViewModel(
        sp.GetRequiredService<SharpClient.Core.Persistence.IWorldStore>()));

var app = builder.Build();

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error", createScopeForErrors: true);

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(typeof(SharpClient.UI.Components.SessionScreen).Assembly);

app.Run();
