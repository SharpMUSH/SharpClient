using SharpClient.Core.Connection;
using SharpClient.Core.Diagnostics;
using SharpClient.Core.Persistence;
using SharpClient.Core.Platform;
using SharpClient.Core.Presentation;
using SharpClient.Core.Sessions;
using SharpClient.Core.Triggers;
using SharpClient.Data;
using SharpClient.UI;
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

// ── Data / persistence ─────────────────────────────────────────────────────
builder.Services.AddScoped<AppDbContext>();
builder.Services.AddScoped<IWorldStore, WorldStore>();
builder.Services.AddScoped<ISessionHistory, SessionHistory>();

// ── Session launcher (real telnet) ─────────────────────────────────────────
builder.Services.AddScoped<ISessionLauncher, TelnetSessionLauncher>();

// ── View models ────────────────────────────────────────────────────────────
// Registered via the shared extension so MAUI and Web stay in lockstep (no host drift).
// Per-view view models are Scoped here to match the Web's per-request scope.
builder.Services.AddSharpClientViewModels(ServiceLifetime.Scoped);

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
