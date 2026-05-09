using MacroRegimeFactorMonitor.Components;
using MacroRegimeFactorMonitor.Data;
using MacroRegimeFactorMonitor.Services;
using MacroRegimeFactorMonitor.Services.Imports;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

ConfigureLogging(builder);
ConfigureServices(builder);

var app = builder.Build();

ConfigureMiddleware(app);
LogSafeStartupConfiguration(app);
await RunStartupSyncAsync(app);
MapEndpoints(app);

app.Run();


static void ConfigureLogging(WebApplicationBuilder builder)
{
    // FRED requires api_key in the query string, so suppress HttpClient
    // informational request/response logs that include full URLs.
    builder.Logging.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);
}

static void ConfigureServices(WebApplicationBuilder builder)
{
    builder.Services.AddRazorComponents()
        .AddInteractiveServerComponents();

    builder.Services.AddDbContextFactory<MacroRegimeDbContext>(options =>
        options.UseConfiguredDatabase(builder.Configuration));
    builder.Services.AddScoped<FactorScoringService>();
    builder.Services.AddScoped<ImportedObservationScoringService>();
    builder.Services.AddScoped<OperationalWorkflowService>();
    builder.Services.AddScoped<JournalService>();
    builder.Services.AddScoped<StartupSyncService>();
    builder.Services.AddScoped<AppConfigurationDiagnosticsService>();
    builder.Services.AddHttpClient<IDataSourceClient, FredDataSourceClient>();
    builder.Services.AddScoped<IDataSourceClient, BlsDataSourceClient>();
    builder.Services.AddScoped<IDataSourceClient, EiaDataSourceClient>();
    builder.Services.AddScoped<IDataSourceClient, TreasuryFiscalDataClient>();
    builder.Services.AddScoped<IDataSourceClientFactory, DataSourceClientFactory>();
    builder.Services.AddScoped<IObservationImportService, ObservationImportService>();
    builder.Services.AddScoped<ImportAdminService>();
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

static void LogSafeStartupConfiguration(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var diagnostics = scope.ServiceProvider.GetRequiredService<AppConfigurationDiagnosticsService>();
    var snapshot = diagnostics.GetSnapshot();

    app.Logger.LogInformation(
        "Startup configuration: Environment={Environment}, DatabaseProvider={DatabaseProvider}, FredApiKeyConfigured={FredApiKeyConfigured}.",
        snapshot.Environment,
        snapshot.DatabaseProvider,
        snapshot.FredApiKeyConfigured);
}

static void MapEndpoints(WebApplication app)
{
    app.MapGet("/health", async (AppConfigurationDiagnosticsService diagnostics, CancellationToken cancellationToken) =>
    {
        var health = await diagnostics.GetHealthAsync(cancellationToken);
        return health.DatabaseReachable
            ? Results.Ok(health)
            : Results.Json(health, statusCode: StatusCodes.Status503ServiceUnavailable);
    });

    app.MapGet("/ready", async (AppConfigurationDiagnosticsService diagnostics, CancellationToken cancellationToken) =>
    {
        var readiness = await diagnostics.GetReadinessAsync(cancellationToken);
        return readiness.DatabaseReady
            ? Results.Ok(readiness)
            : Results.Json(readiness, statusCode: StatusCodes.Status503ServiceUnavailable);
    });

    app.MapRazorComponents<App>()
        .AddInteractiveServerRenderMode();
}
