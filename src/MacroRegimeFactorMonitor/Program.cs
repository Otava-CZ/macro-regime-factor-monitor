using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
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
    builder.Services.AddScoped<ModelSnapshotService>();
    builder.Services.AddSingleton<OperatorActionCooldown>();
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

    app.MapGet("/api/model/snapshot", async (ModelSnapshotService snapshotService, CancellationToken cancellationToken) =>
    {
        var snapshot = await snapshotService.GetSnapshotAsync(cancellationToken);
        return Results.Ok(snapshot);
    });

    app.MapGet("/ops/actions/imports/fred/refresh-active", async (
        string? confirm,
        IConfiguration configuration,
        ImportAdminService importAdminService,
        OperatorActionCooldown cooldown,
        CancellationToken cancellationToken) =>
    {
        if (!OperatorActionsEnabled(configuration))
        {
            return OperatorHtmlResult("Operator actions disabled", "Operator actions are disabled.", StatusCodes.Status403Forbidden);
        }

        if (!string.Equals(confirm, "refresh-active-fred", StringComparison.Ordinal))
        {
            return OperatorHtmlResult(
                "Confirmation required",
                "Confirmation is required. Add confirm=refresh-active-fred to run this manual action.",
                StatusCodes.Status400BadRequest);
        }

        var cooldownResult = cooldown.TryStart("refresh-active-fred", DateTimeOffset.UtcNow);
        if (!cooldownResult.IsAllowed)
        {
            return OperatorHtmlResult(
                "Cooldown active",
                $"The refresh-active-fred manual action is on cooldown. Try again in {Math.Ceiling(cooldownResult.RetryAfter.TotalSeconds)} seconds.",
                StatusCodes.Status429TooManyRequests);
        }

        try
        {
            var result = await importAdminService.RefreshAllActiveFredSeriesAsync(cancellationToken);
            return OperatorHtmlResult("Run active FRED imports", BuildImportActionResultHtml(result));
        }
        catch (Exception exception)
        {
            return OperatorHtmlResult(
                "Run active FRED imports failed",
                $"<p>Status: Failed</p><p>Safe error summary: {SafeHtml(importAdminService.CreateSafeErrorSummary(exception))}</p>",
                StatusCodes.Status500InternalServerError);
        }
    });

    app.MapGet("/ops/actions/scoring/recalculate-imported-manual", async (
        string? confirm,
        string? scoreDate,
        IConfiguration configuration,
        ImportedObservationScoringService scoringService,
        OperatorActionCooldown cooldown,
        CancellationToken cancellationToken) =>
    {
        if (!OperatorActionsEnabled(configuration))
        {
            return OperatorHtmlResult("Operator actions disabled", "Operator actions are disabled.", StatusCodes.Status403Forbidden);
        }

        if (!string.Equals(confirm, "recalculate-imported-manual", StringComparison.Ordinal))
        {
            return OperatorHtmlResult(
                "Confirmation required",
                "Confirmation is required. Add confirm=recalculate-imported-manual to run this manual action.",
                StatusCodes.Status400BadRequest);
        }

        DateOnly resolvedScoreDate;
        if (!string.IsNullOrWhiteSpace(scoreDate))
        {
            if (!DateOnly.TryParseExact(scoreDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out resolvedScoreDate))
            {
                return OperatorHtmlResult(
                    "Invalid score date",
                    "The scoreDate query value must use YYYY-MM-DD format.",
                    StatusCodes.Status400BadRequest);
            }
        }
        else
        {
            var latestObservationDate = await scoringService.GetLatestImportedObservationDateAsync(cancellationToken);
            if (latestObservationDate is null)
            {
                return OperatorHtmlResult(
                    "No imported observations",
                    "No imported observations available; run import first.",
                    StatusCodes.Status409Conflict);
            }

            resolvedScoreDate = latestObservationDate.Value;
        }

        var cooldownResult = cooldown.TryStart("recalculate-imported-manual", DateTimeOffset.UtcNow);
        if (!cooldownResult.IsAllowed)
        {
            return OperatorHtmlResult(
                "Cooldown active",
                $"The recalculate-imported-manual action is on cooldown. Try again in {Math.Ceiling(cooldownResult.RetryAfter.TotalSeconds)} seconds.",
                StatusCodes.Status429TooManyRequests);
        }

        try
        {
            var result = await scoringService.RecalculateCurrentScoresAsync(resolvedScoreDate, cancellationToken);
            return OperatorHtmlResult("Recalculate ImportedManual scores", BuildScoringActionResultHtml(result));
        }
        catch (Exception exception)
        {
            return OperatorHtmlResult(
                "Recalculate ImportedManual scores failed",
                $"<p>Status: Failed</p><p>Safe error summary: {SafeHtml(CreateSafeErrorSummary(exception, "Scoring workflow failed."))}</p>",
                StatusCodes.Status500InternalServerError);
        }
    });

    app.MapRazorComponents<App>()
        .AddInteractiveServerRenderMode();
}

static bool OperatorActionsEnabled(IConfiguration configuration) =>
    configuration.GetValue<bool>("OperatorActions:Enabled");

static IResult OperatorHtmlResult(string title, string bodyHtml, int statusCode = StatusCodes.Status200OK)
{
    var html = new StringBuilder()
        .AppendLine("<!doctype html>")
        .AppendLine("<html lang=\"en\">")
        .AppendLine("<head>")
        .Append("<title>").Append(SafeHtml(title)).AppendLine("</title>")
        .AppendLine("<meta charset=\"utf-8\">")
        .AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">")
        .AppendLine("<style>body{font-family:system-ui,-apple-system,BlinkMacSystemFont,'Segoe UI',sans-serif;line-height:1.5;margin:2rem;max-width:1100px}table{border-collapse:collapse;width:100%;margin:1rem 0}th,td{border:1px solid #ddd;padding:.45rem;text-align:left;vertical-align:top}th{background:#f5f5f5}.links{display:flex;gap:1rem;flex-wrap:wrap;margin:1.5rem 0}.warning{color:#8a5a00}.error{color:#9f1239}code{background:#f5f5f5;padding:.1rem .25rem}</style>")
        .AppendLine("</head>")
        .AppendLine("<body>")
        .Append("<h1>").Append(SafeHtml(title)).AppendLine("</h1>")
        .AppendLine(bodyHtml)
        .AppendLine("<div class=\"links\">")
        .AppendLine("<a href=\"/ops\">Back to /ops</a>")
        .AppendLine("<a href=\"/api/model/snapshot\">/api/model/snapshot</a>")
        .AppendLine("<a href=\"/dashboard\">/dashboard</a>")
        .AppendLine("<a href=\"/system\">/system</a>")
        .AppendLine("</div>")
        .AppendLine("</body>")
        .AppendLine("</html>")
        .ToString();

    return Results.Content(html, "text/html; charset=utf-8", statusCode: statusCode);
}

static string BuildImportActionResultHtml(ImportBatchResult result)
{
    var html = new StringBuilder()
        .AppendLine("<p>Status: ").Append(SafeHtml(result.Status)).AppendLine("</p>")
        .AppendLine("<table><tbody>")
        .Append("<tr><th>StartedAtUtc</th><td>").Append(SafeHtml(FormatDateTime(result.StartedAtUtc))).AppendLine("</td></tr>")
        .Append("<tr><th>FinishedAtUtc</th><td>").Append(SafeHtml(FormatDateTime(result.FinishedAtUtc))).AppendLine("</td></tr>")
        .Append("<tr><th>Series attempted</th><td>").Append(result.SeriesAttempted).AppendLine("</td></tr>")
        .Append("<tr><th>Series completed</th><td>").Append(result.SeriesCompleted).AppendLine("</td></tr>")
        .Append("<tr><th>Series failed</th><td>").Append(result.SeriesFailed).AppendLine("</td></tr>")
        .Append("<tr><th>Rows read</th><td>").Append(result.TotalRowsRead).AppendLine("</td></tr>")
        .Append("<tr><th>Rows inserted</th><td>").Append(result.TotalRowsInserted).AppendLine("</td></tr>")
        .Append("<tr><th>Rows updated</th><td>").Append(result.TotalRowsUpdated).AppendLine("</td></tr>")
        .AppendLine("</tbody></table>")
        .AppendLine("<h2>Per-series result</h2>")
        .AppendLine("<table><thead><tr><th>External series</th><th>Window</th><th>Status</th><th>Rows read</th><th>Rows inserted</th><th>Rows updated</th><th>Rows skipped</th><th>Warnings</th><th>Safe error summary</th></tr></thead><tbody>");

    foreach (var series in result.SeriesResults)
    {
        html.Append("<tr><td><code>").Append(SafeHtml(series.ExternalSeriesCode)).Append("</code></td>")
            .Append("<td>").Append(SafeHtml($"{series.FromDate:yyyy-MM-dd} to {series.ToDate:yyyy-MM-dd}")).Append("</td>")
            .Append("<td>").Append(SafeHtml(series.Status)).Append("</td>")
            .Append("<td>").Append(series.RowsRead).Append("</td>")
            .Append("<td>").Append(series.RowsInserted).Append("</td>")
            .Append("<td>").Append(series.RowsUpdated).Append("</td>")
            .Append("<td>").Append(series.RowsSkipped).Append("</td>")
            .Append("<td>").Append(SafeHtml(FormatList(series.Warnings))).Append("</td>")
            .Append("<td>").Append(SafeHtml(series.ErrorMessage)).AppendLine("</td></tr>");
    }

    html.AppendLine("</tbody></table>");
    return html.ToString();
}

static string BuildScoringActionResultHtml(ImportedObservationScoringResult result)
{
    var html = new StringBuilder()
        .AppendLine("<p>Status: Completed</p>")
        .AppendLine("<table><tbody>")
        .Append("<tr><th>ScoreDate</th><td>").Append(SafeHtml(result.ScoreDate.ToString("yyyy-MM-dd"))).AppendLine("</td></tr>")
        .Append("<tr><th>DataMode</th><td>").Append(SafeHtml(result.DataMode)).AppendLine("</td></tr>")
        .Append("<tr><th>ScoringModelVersion</th><td>").Append(SafeHtml(result.ScoringModelVersion)).AppendLine("</td></tr>")
        .Append("<tr><th>CalculatedAtUtc</th><td>").Append(SafeHtml(FormatDateTime(result.CalculatedAtUtc))).AppendLine("</td></tr>")
        .Append("<tr><th>Scores inserted</th><td>").Append(result.ScoresInserted).AppendLine("</td></tr>")
        .Append("<tr><th>Scores updated</th><td>").Append(result.ScoresUpdated).AppendLine("</td></tr>")
        .Append("<tr><th>Scores failed</th><td>").Append(result.ScoresFailed).AppendLine("</td></tr>")
        .Append("<tr><th>Warnings</th><td>").Append(SafeHtml(FormatList(result.Warnings))).AppendLine("</td></tr>")
        .AppendLine("</tbody></table>")
        .AppendLine("<h2>Factor score rows</h2>")
        .AppendLine("<table><thead><tr><th>Macro factor</th><th>Raw score</th><th>Weighted score</th><th>Regime impact</th><th>Source observations</th><th>Source observation date</th><th>Source observation value</th><th>Data quality</th><th>Scoring confidence</th><th>Calculation notes</th></tr></thead><tbody>");

    foreach (var row in result.ScoreRows)
    {
        html.Append("<tr><td>").Append(SafeHtml(row.MacroFactor)).Append("</td>")
            .Append("<td>").Append(SafeHtml(row.RawScore.ToString("0.##", CultureInfo.InvariantCulture))).Append("</td>")
            .Append("<td>").Append(SafeHtml(row.WeightedScore.ToString("0.##", CultureInfo.InvariantCulture))).Append("</td>")
            .Append("<td>").Append(SafeHtml(row.RegimeImpact)).Append("</td>")
            .Append("<td>").Append(row.SourceObservationCount).Append("</td>")
            .Append("<td>").Append(SafeHtml(FormatDate(row.SourceObservationDate))).Append("</td>")
            .Append("<td>").Append(SafeHtml(row.SourceObservationValue?.ToString("0.##", CultureInfo.InvariantCulture) ?? "—")).Append("</td>")
            .Append("<td>").Append(SafeHtml(FormatNullableText(row.DataQualityStatus))).Append("</td>")
            .Append("<td>").Append(SafeHtml(FormatNullableText(row.ScoringConfidence))).Append("</td>")
            .Append("<td>").Append(SafeHtml(row.CalculationNotes)).AppendLine("</td></tr>");
    }

    html.AppendLine("</tbody></table>");
    return html.ToString();
}

static string SafeHtml(string? value) =>
    HtmlEncoder.Default.Encode(ScrubSensitiveValues(value ?? string.Empty));

static string ScrubSensitiveValues(string value)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        return string.Empty;
    }

    var withoutApiKey = Regex.Replace(value, "(?i)(api_key=)[^&\\s]+", "$1[redacted]");
    return Regex.Replace(withoutApiKey, "(?i)(apikey=)[^&\\s]+", "$1[redacted]");
}

static string CreateSafeErrorSummary(Exception exception, string fallbackMessage)
{
    const int maximumLength = 500;
    var message = exception.Message;
    if (string.IsNullOrWhiteSpace(message))
    {
        message = fallbackMessage;
    }

    message = ScrubSensitiveValues(message.ReplaceLineEndings(" ").Trim());
    return message.Length <= maximumLength
        ? message
        : string.Concat(message.AsSpan(0, maximumLength), "...");
}

static string FormatDate(DateOnly? date) => date?.ToString("yyyy-MM-dd") ?? "—";

static string FormatDateTime(DateTime? dateTime) =>
    dateTime?.ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture) ?? "—";

static string FormatNullableText(string? value) => string.IsNullOrWhiteSpace(value) ? "—" : value;

static string FormatList(IEnumerable<string> values)
{
    var safeValues = values
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Select(ScrubSensitiveValues)
        .ToList();

    return safeValues.Count == 0 ? "—" : string.Join("; ", safeValues);
}
