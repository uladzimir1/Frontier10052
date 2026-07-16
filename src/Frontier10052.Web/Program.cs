using Frontier10052.Gameplay.Journey;
using Frontier10052.Gameplay.Launcher;
using Frontier10052.Gameplay.Operations;
using Frontier10052.Gameplay.Persistence;
using Frontier10052.Gameplay.Turnaround;
using Frontier10052.Infrastructure;
using Frontier10052.Web.Components;

var builder = WebApplication.CreateBuilder(args);

// Release-mode local previews and container runs execute from the build output,
// so explicitly load the static-web-assets manifest that contains Blazor's
// framework files as well as the authored site assets.
builder.WebHost.UseStaticWebAssets();

builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();

string saveDirectory = builder.Configuration["Frontier10052:SavesDirectory"]
    ?? Path.Combine(builder.Environment.ContentRootPath, "App_Data", "saves");
builder.Services.AddSingleton<IGameSaveStore>(_ => new JsonGameSaveStore(saveDirectory));
builder.Services.AddSingleton<GameSessionCoordinator>();
builder.Services.AddSingleton<IStationOperationsService, StationOperationsService>();
builder.Services.AddSingleton<IJourneyService, JourneyService>();
builder.Services.AddSingleton<ITurnaroundService, TurnaroundService>();
builder.Services.AddSingleton<ILauncherSnapshotQuery, StationLauncherSnapshotQuery>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
