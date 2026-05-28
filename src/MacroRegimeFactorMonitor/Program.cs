using MacroRegimeFactorMonitor.Components;
using MacroRegimeFactorMonitor.Data;
using MacroRegimeFactorMonitor.Services;
using MacroRegimeFactorMonitor.Services.Imports;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

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
    app.UseTemporaryAccessGate();
    app.UseAntiforgery();
}

static async Task RunStartupSyncAsync(WebApplication app)
{
    var failFast = app.Configuration.GetValue("StartupSync:FailFast", app.Environment.IsDevelopment());

    try
    {
        await using var scope = app.Services.CreateAsyncScope();
        var startupSync = scope.ServiceProvider.GetRequiredService<StartupSyncService>();
        await startupSync.RunAsync();
    }
    catch (Exception ex)
    {
        app.Logger.LogError(
            ex,
            "Startup sync failed. FailFast={FailFast}. The application will {StartupBehavior}.",
            failFast,
            failFast ? "stop" : "continue running in degraded mode");

        if (failFast)
        {
            throw;
        }
    }
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
    app.MapGet("/ping", () => Results.Ok(new
    {
        status = "Running",
        appName = "MacroRegimeFactorMonitor",
        environment = app.Environment.EnvironmentName,
        utcNow = DateTimeOffset.UtcNow
    }));

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

static class TemporaryAccessGateMiddlewareExtensions
{
    private const string CookieName = "MacroRegimeTemporaryAccess";
    private const string AccessTokenQueryKey = "access_token";

    public static IApplicationBuilder UseTemporaryAccessGate(this WebApplication app)
    {
        var enabled = app.Configuration.GetValue("TemporaryAccess:Enabled", false);
        var configuredToken = app.Configuration["TemporaryAccess:Token"];

        if (!enabled || string.IsNullOrWhiteSpace(configuredToken))
        {
            if (enabled)
            {
                app.Logger.LogWarning("Temporary access gate is enabled but no token is configured. Access gate remains disabled.");
            }

            return app;
        }

        app.Logger.LogInformation("Temporary access gate enabled for preview access. Token value is not logged.");

        app.Use(async (context, next) =>
        {
            var queryToken = context.Request.Query[AccessTokenQueryKey].FirstOrDefault();

            if (IsValidToken(queryToken, configuredToken))
            {
                context.Response.Cookies.Append(
                    CookieName,
                    configuredToken,
                    new CookieOptions
                    {
                        HttpOnly = true,
                        IsEssential = true,
                        SameSite = SameSiteMode.Lax,
                        Expires = DateTimeOffset.UtcNow.AddHours(12)
                    });

                var cleanUrl = BuildUrlWithoutAccessToken(context);
                context.Response.Redirect(cleanUrl);
                return;
            }

            if (IsValidToken(context.Request.Cookies[CookieName], configuredToken))
            {
                await next();
                return;
            }

            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "text/html; charset=utf-8";
            await context.Response.WriteAsync("""
                <!doctype html>
                <html lang="en">
                <head><meta charset="utf-8"><title>Access token required</title></head>
                <body style="font-family: sans-serif; margin: 2rem; line-height: 1.4;">
                    <h1>Access token required</h1>
                    <p>This temporary preview deployment requires an access token.</p>
                </body>
                </html>
                """);
        });

        return app;
    }

    private static bool IsValidToken(string? candidateToken, string configuredToken)
    {
        if (string.IsNullOrEmpty(candidateToken))
        {
            return false;
        }

        var candidateBytes = Encoding.UTF8.GetBytes(candidateToken);
        var configuredBytes = Encoding.UTF8.GetBytes(configuredToken);

        return candidateBytes.Length == configuredBytes.Length
            && CryptographicOperations.FixedTimeEquals(candidateBytes, configuredBytes);
    }

    private static string BuildUrlWithoutAccessToken(HttpContext context)
    {
        var query = new QueryBuilder(
            context.Request.Query
                .Where(parameter => !string.Equals(parameter.Key, AccessTokenQueryKey, StringComparison.OrdinalIgnoreCase))
                .SelectMany(parameter => parameter.Value.Select(value => new KeyValuePair<string, string?>(parameter.Key, value))));

        return UriHelper.BuildRelative(
            context.Request.PathBase,
            context.Request.Path,
            query.ToQueryString(),
            context.Request.Fragment);
    }
}
