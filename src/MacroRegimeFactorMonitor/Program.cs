using MacroRegimeFactorMonitor.Components;
using MacroRegimeFactorMonitor.Data;
using MacroRegimeFactorMonitor.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

ConfigureServices(builder);

var app = builder.Build();

ConfigureMiddleware(app);
await RunStartupSyncAsync(app);
MapEndpoints(app);

app.Run();

static void ConfigureServices(WebApplicationBuilder builder)
{
    builder.Services.AddRazorComponents()
        .AddInteractiveServerComponents();

    builder.Services.AddDbContextFactory<MacroRegimeDbContext>(options =>
        options.UseConfiguredDatabase(builder.Configuration));
    builder.Services.AddScoped<FactorScoringService>();
    builder.Services.AddScoped<JournalService>();
    builder.Services.AddScoped<StartupSyncService>();
}

static void ConfigureMiddleware(WebApplication app)
{
    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error", createScopeForErrors: true);
        app.UseHsts();
    }

    app.UseHttpsRedirection();
    app.UseStaticFiles();
    app.UseAntiforgery();
}

static async Task RunStartupSyncAsync(WebApplication app)
{
    await using var scope = app.Services.CreateAsyncScope();
    var startupSync = scope.ServiceProvider.GetRequiredService<StartupSyncService>();
    await startupSync.RunAsync();
}

static void MapEndpoints(WebApplication app)
{
    app.MapRazorComponents<App>()
        .AddInteractiveServerRenderMode();
}
