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
vesper.AppendLine("\u001b[1mRED SECTOR — THE DROME\u001b[0m");
vesper.AppendLine("A vast underground arena carved from volcanic basalt, lit by chemical torches.");
vesper.AppendLine("");
vesper.AppendLine("\u001b[31mThe walls are slick with condensation and old blood.\u001b[0m");
vesper.AppendLine("\u001b[92mServer:\u001b[0m  Sindome v3.4.1  \u001b[92m[online]\u001b[0m");
vesper.AppendLine("\u001b[38;5;208mWARNING:\u001b[0m  Low-visibility zone — torches flicker at zone boundary.");
vesper.AppendLine("\u001b[1m\u001b[36mThe Arena Master\u001b[0m steps from the shadows and surveys the pit.");
vesper.AppendLine("\u001b[33mGold\u001b[0m glints against \u001b[34mblue\u001b[0m steel; the crowd roars.");
vesper.AppendLine("\u001b[93m[SYSTEM]\u001b[0m  Connection established.  Type \u001b[4mhelp\u001b[0m to begin.");
vesper.AppendLine("\u001b[35mA resonant chime echoes through the stone passages.\u001b[0m");
vesper.AppendLine("");
vesper.AppendLine("\u001b[38;5;118mHP: 320/320  \u001b[38;5;208mMP: 140/200  \u001b[38;5;87mSP:  75/100\u001b[0m");
vesper.AppendLine("You are in the Gate Tunnel.  Exits: [north] [west]");
vesper.AppendLine("");
vesper.AppendLine("\u001b[1m\u001b[97m[ Combat Log ]\u001b[0m");
vesper.AppendLine("\u001b[91mDrakar strikes you for 42 points of damage!\u001b[0m");
vesper.AppendLine("\u001b[92mYou strike Drakar for 31 points of damage.\u001b[0m");
vesper.AppendLine("\u001b[7m ROUNDTIME: 3s \u001b[0m");

var thorne = new DemoSession
{
    CharacterName = "Thorne",
    WorldName = "GrapevineMUD",
    State = ConnectionState.Connecting,
};
thorne.AppendLine("\u001b[93m[SYSTEM]\u001b[0m  Connecting to GrapevineMUD…");
thorne.AppendLine("\u001b[90mEstablishing secure channel…\u001b[0m");

var doran = new DemoSession
{
    CharacterName = "Doran",
    WorldName = "BatMUD",
    State = ConnectionState.Error,
};
doran.AppendLine("\u001b[91m[ERROR]\u001b[0m  Connection lost: remote host closed the connection.");
doran.AppendLine("\u001b[90mLast seen: BatMUD Gate, south of Market Square.\u001b[0m");

mgr.Add(vesper);
mgr.Add(thorne);
mgr.Add(doran);
vm.Select(vesper);

app.Run();
