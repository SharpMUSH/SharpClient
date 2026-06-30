using SharpClient.Core.Connection;
using SharpClient.Core.Presentation;
using SharpClient.Core.Sessions;
using SharpClient.Web;
using SharpClient.Web.Components;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddSingleton<SessionManager>();
builder.Services.AddSingleton<ISessionManager>(sp => sp.GetRequiredService<SessionManager>());
builder.Services.AddSingleton<SessionsViewModel>(sp =>
    new SessionsViewModel(sp.GetRequiredService<ISessionManager>()));

// World Manager: EF persistence + in-memory secrets + demo launcher.
builder.Services.AddSingleton<SharpClient.Core.Platform.IAppStorage, WebAppStorage>();
builder.Services.AddSingleton<SharpClient.Core.Persistence.ISecretStore, WebSecretStore>();
builder.Services.AddScoped<SharpClient.Data.AppDbContext>();
builder.Services.AddScoped<SharpClient.Core.Persistence.IWorldStore, SharpClient.Data.WorldStore>();
builder.Services.AddScoped<ISessionLauncher, DemoSessionLauncher>();
builder.Services.AddScoped<WorldManagerViewModel>(sp => new WorldManagerViewModel(
    sp.GetRequiredService<SharpClient.Core.Persistence.IWorldStore>(),
    sp.GetRequiredService<SharpClient.Core.Persistence.ISecretStore>(),
    sp.GetRequiredService<ISessionManager>(),
    sp.GetRequiredService<ISessionLauncher>()));

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Seed demo sessions.
var mgr = app.Services.GetRequiredService<SessionManager>();
var vm = app.Services.GetRequiredService<SessionsViewModel>();

var vesper = new DemoSession
{
    CharacterName = "Vesper",
    WorldName = "Sindome",
    State = ConnectionState.Connected,
};
vesper.AppendLine("[1mRED SECTOR — THE DROME[0m");
vesper.AppendLine("A vast underground arena carved from volcanic basalt, lit by chemical torches.");
vesper.AppendLine("");
vesper.AppendLine("[31mThe walls are slick with condensation and old blood.[0m");
vesper.AppendLine("[92mServer:[0m  Sindome v3.4.1  [92m[online][0m");
vesper.AppendLine("[38;5;208mWARNING:[0m  Low-visibility zone — torches flicker at zone boundary.");
vesper.AppendLine("[1m[36mThe Arena Master[0m steps from the shadows and surveys the pit.");
vesper.AppendLine("[33mGold[0m glints against [34mblue[0m steel; the crowd roars.");
vesper.AppendLine("[93m[SYSTEM][0m  Connection established.  Type [4mhelp[0m to begin.");
vesper.AppendLine("[35mA resonant chime echoes through the stone passages.[0m");
vesper.AppendLine("");
vesper.AppendLine("[38;5;118mHP: 320/320  [38;5;208mMP: 140/200  [38;5;87mSP:  75/100[0m");
vesper.AppendLine("You are in the Gate Tunnel.  Exits: [north] [west]");
vesper.AppendLine("");
vesper.AppendLine("[1m[97m[ Combat Log ][0m");
vesper.AppendLine("[91mDrakar strikes you for 42 points of damage![0m");
vesper.AppendLine("[92mYou strike Drakar for 31 points of damage.[0m");
vesper.AppendLine("[7m ROUNDTIME: 3s [0m");

var thorne = new DemoSession
{
    CharacterName = "Thorne",
    WorldName = "GrapevineMUD",
    State = ConnectionState.Connecting,
};
thorne.AppendLine("[93m[SYSTEM][0m  Connecting to GrapevineMUD…");
thorne.AppendLine("[90mEstablishing secure channel…[0m");

var doran = new DemoSession
{
    CharacterName = "Doran",
    WorldName = "BatMUD",
    State = ConnectionState.Error,
};
doran.AppendLine("[91m[ERROR][0m  Connection lost: remote host closed the connection.");
doran.AppendLine("[90mLast seen: BatMUD Gate, south of Market Square.[0m");

mgr.Add(vesper);
mgr.Add(thorne);
mgr.Add(doran);
vm.Select(vesper);

app.Run();
