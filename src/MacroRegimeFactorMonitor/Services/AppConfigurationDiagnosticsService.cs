using MacroRegimeFactorMonitor.Data;
using Microsoft.EntityFrameworkCore;

namespace MacroRegimeFactorMonitor.Services;

public sealed class AppConfigurationDiagnosticsService(
    IConfiguration configuration,
    IWebHostEnvironment environment,
    IDbContextFactory<MacroRegimeDbContext> dbContextFactory,
    ILogger<AppConfigurationDiagnosticsService> logger)
{
    private const string DefaultFredBaseUrl = "https://api.stlouisfed.org/fred";
    private const string FredSourceName = "FRED";
    private const string CompletedStatus = "Completed";
    private const string ImportedManualDataMode = "ImportedManual";

    public AppConfigurationDiagnosticsSnapshot GetSnapshot()
    {
        var providerValue = configuration["Database:Provider"];
        var databaseProviderConfigured = !string.IsNullOrWhiteSpace(providerValue);
        var databaseProvider = DatabaseProvider.IsPostgres(configuration) ? "Postgres" : "Sqlite";
        var connectionString = configuration.GetConnectionString(DatabaseProvider.ConnectionStringName);
        var fredBaseUrl = configuration["Fred:BaseUrl"];

        return new AppConfigurationDiagnosticsSnapshot
        {
            AppName = environment.ApplicationName,
            Environment = environment.EnvironmentName,
            DatabaseProvider = databaseProvider,
            DatabaseProviderConfigured = databaseProviderConfigured,
            DatabaseConnectionConfigured = !string.IsNullOrWhiteSpace(connectionString),
            FredApiKeyConfigured = !string.IsNullOrWhiteSpace(configuration["Fred:ApiKey"]),
            FredBaseUrlConfigured = !string.IsNullOrWhiteSpace(fredBaseUrl),
            FredBaseUrlSource = string.IsNullOrWhiteSpace(fredBaseUrl) ? "Default" : "Configured",
            FredBaseUrlEffectiveConfigured = true
        };
    }

    public async Task<DatabaseReachabilityResult> CheckDatabaseAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var connection = dbContext.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
            {
                await connection.OpenAsync(cancellationToken);
            }

            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            var result = await command.ExecuteScalarAsync(cancellationToken);

            return new DatabaseReachabilityResult(
                IsReachable: result is not null,
                Message: result is null ? "Database returned no result for SELECT 1." : "Database connection opened and SELECT 1 succeeded.");
        }
        catch (Exception ex)
        {
            logger.LogWarning("Database diagnostics check failed. ExceptionType={ExceptionType}.", ex.GetType().Name);
            return new DatabaseReachabilityResult(IsReachable: false, Message: "Database connection check failed.");
        }
    }

    public async Task<HealthResponse> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = GetSnapshot();
        var database = await CheckDatabaseAsync(cancellationToken);
        var checks = new List<HealthCheckItem>
        {
            new("database", database.IsReachable ? "Healthy" : "Unhealthy", database.Message),
            new("databaseProvider", snapshot.DatabaseProviderConfigured ? "Healthy" : "Degraded", snapshot.DatabaseProviderConfigured
                ? "Database:Provider is configured."
                : "Database:Provider is not configured; SQLite fallback will be used."),
            new("fredApiKey", snapshot.FredApiKeyConfigured ? "Healthy" : "Degraded", snapshot.FredApiKeyConfigured
                ? "FRED API key is configured."
                : "Fred:ApiKey is missing; manual FRED imports will fail until configured."),
            new("fredBaseUrl", "Healthy", snapshot.FredBaseUrlConfigured
                ? "FRED base URL is configured."
                : $"FRED base URL is not configured; default {DefaultFredBaseUrl} will be used.")
        };

        var status = database.IsReachable && checks.All(check => check.Status == "Healthy") ? "Healthy" : "Degraded";

        return new HealthResponse
        {
            Status = status,
            AppName = snapshot.AppName,
            Environment = snapshot.Environment,
            DatabaseProvider = snapshot.DatabaseProvider,
            DatabaseReachable = database.IsReachable,
            DatabaseProviderConfigured = snapshot.DatabaseProviderConfigured,
            FredApiKeyConfigured = snapshot.FredApiKeyConfigured,
            FredBaseUrlConfigured = snapshot.FredBaseUrlEffectiveConfigured,
            UtcNow = DateTime.UtcNow,
            Checks = checks
        };
    }

    public async Task<ReadinessResponse> GetReadinessAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = GetSnapshot();
        var database = await CheckDatabaseAsync(cancellationToken);
        if (!database.IsReachable)
        {
            return new ReadinessResponse
            {
                Ready = false,
                DatabaseReady = false,
                FredConfigured = snapshot.FredApiKeyConfigured && snapshot.FredBaseUrlEffectiveConfigured,
                Warnings = [$"Database is unavailable: {database.Message}"]
            };
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var activeFredSeriesCount = await dbContext.ExternalSeries
            .AsNoTracking()
            .Include(series => series.DataSource)
            .CountAsync(series => series.IsActive
                && series.DataSource != null
                && series.DataSource.Name == FredSourceName,
                cancellationToken);

        var latestSuccessfulImportUtc = await dbContext.DataImportRuns
            .AsNoTracking()
            .Include(run => run.DataSource)
            .Where(run => run.DataSource != null
                && run.DataSource.Name == FredSourceName
                && run.Status == CompletedStatus)
            .MaxAsync(run => (DateTime?)(run.FinishedAtUtc ?? run.StartedAtUtc), cancellationToken);

        var latestImportedManualScore = await dbContext.FactorScores
            .AsNoTracking()
            .Where(score => score.DataMode == ImportedManualDataMode)
            .OrderByDescending(score => score.ScoreDate)
            .ThenByDescending(score => score.CalculatedAtUtc)
            .ThenByDescending(score => score.Id)
            .Select(score => new
            {
                score.ScoreDate,
                score.ScoringModelVersion
            })
            .FirstOrDefaultAsync(cancellationToken);

        var warnings = new List<string>();
        if (!snapshot.FredApiKeyConfigured)
        {
            warnings.Add("Fred:ApiKey is missing; manual FRED imports are not ready.");
        }

        if (activeFredSeriesCount == 0)
        {
            warnings.Add("No active FRED series are configured.");
        }

        if (latestSuccessfulImportUtc is null)
        {
            warnings.Add("No successful FRED import has been recorded yet.");
        }

        if (latestImportedManualScore is null)
        {
            warnings.Add("No ImportedManual scores have been recorded yet.");
        }

        return new ReadinessResponse
        {
            Ready = warnings.Count == 0,
            DatabaseReady = true,
            FredConfigured = snapshot.FredApiKeyConfigured && snapshot.FredBaseUrlEffectiveConfigured,
            ActiveFredSeriesCount = activeFredSeriesCount,
            LatestSuccessfulImportUtc = latestSuccessfulImportUtc,
            LatestImportedManualScoreDate = latestImportedManualScore?.ScoreDate,
            LatestImportedManualScoringModelVersion = latestImportedManualScore?.ScoringModelVersion,
            Warnings = warnings
        };
    }

    public async Task<SystemDiagnosticsResponse> GetSystemDiagnosticsAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = GetSnapshot();
        var database = await CheckDatabaseAsync(cancellationToken);
        var readiness = database.IsReachable
            ? await GetReadinessAsync(cancellationToken)
            : new ReadinessResponse
            {
                Ready = false,
                DatabaseReady = false,
                FredConfigured = snapshot.FredApiKeyConfigured && snapshot.FredBaseUrlEffectiveConfigured,
                Warnings = [$"Database is unavailable: {database.Message}"]
            };

        StartupSyncSummary? startupSync = null;
        if (database.IsReachable)
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            startupSync = await dbContext.StartupSyncRuns
                .AsNoTracking()
                .OrderByDescending(run => run.StartedAtUtc)
                .ThenByDescending(run => run.Id)
                .Select(run => new StartupSyncSummary
                {
                    StartedAtUtc = run.StartedAtUtc,
                    FinishedAtUtc = run.FinishedAtUtc,
                    Status = run.Status,
                    Message = run.Message,
                    ErrorMessage = run.ErrorMessage
                })
                .FirstOrDefaultAsync(cancellationToken);
        }

        return new SystemDiagnosticsResponse
        {
            Configuration = snapshot,
            DatabaseReachable = database.IsReachable,
            DatabaseMessage = database.Message,
            StartupSync = startupSync,
            Readiness = readiness
        };
    }
}

public sealed class AppConfigurationDiagnosticsSnapshot
{
    public required string AppName { get; init; }
    public required string Environment { get; init; }
    public required string DatabaseProvider { get; init; }
    public bool DatabaseProviderConfigured { get; init; }
    public bool DatabaseConnectionConfigured { get; init; }
    public bool FredApiKeyConfigured { get; init; }
    public bool FredBaseUrlConfigured { get; init; }
    public required string FredBaseUrlSource { get; init; }
    public bool FredBaseUrlEffectiveConfigured { get; init; }
}

public sealed record DatabaseReachabilityResult(bool IsReachable, string Message);

public sealed record HealthCheckItem(string Name, string Status, string Message);

public sealed class HealthResponse
{
    public required string Status { get; init; }
    public required string AppName { get; init; }
    public required string Environment { get; init; }
    public required string DatabaseProvider { get; init; }
    public bool DatabaseReachable { get; init; }
    public bool DatabaseProviderConfigured { get; init; }
    public bool FredApiKeyConfigured { get; init; }
    public bool FredBaseUrlConfigured { get; init; }
    public DateTime UtcNow { get; init; }
    public IReadOnlyList<HealthCheckItem> Checks { get; init; } = [];
}

public sealed class ReadinessResponse
{
    public bool Ready { get; init; }
    public bool DatabaseReady { get; init; }
    public bool FredConfigured { get; init; }
    public int ActiveFredSeriesCount { get; init; }
    public DateTime? LatestSuccessfulImportUtc { get; init; }
    public DateOnly? LatestImportedManualScoreDate { get; init; }
    public string? LatestImportedManualScoringModelVersion { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = [];
}

public sealed class SystemDiagnosticsResponse
{
    public required AppConfigurationDiagnosticsSnapshot Configuration { get; init; }
    public bool DatabaseReachable { get; init; }
    public required string DatabaseMessage { get; init; }
    public StartupSyncSummary? StartupSync { get; init; }
    public required ReadinessResponse Readiness { get; init; }
}

public sealed class StartupSyncSummary
{
    public DateTime StartedAtUtc { get; init; }
    public DateTime? FinishedAtUtc { get; init; }
    public required string Status { get; init; }
    public string Message { get; init; } = string.Empty;
    public string ErrorMessage { get; init; } = string.Empty;
}
