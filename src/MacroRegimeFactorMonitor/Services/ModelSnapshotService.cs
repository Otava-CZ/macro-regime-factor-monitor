using MacroRegimeFactorMonitor.Data;
using Microsoft.EntityFrameworkCore;

namespace MacroRegimeFactorMonitor.Services;

public sealed class ModelSnapshotService(
    FactorScoringService factorScoringService,
    AppConfigurationDiagnosticsService diagnosticsService,
    IDbContextFactory<MacroRegimeDbContext> dbFactory,
    IConfiguration configuration)
{
    public async Task<ModelSnapshotResponse> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var configurationSnapshot = diagnosticsService.GetSnapshot();
        var deployment = GetDeploymentMetadata(configurationSnapshot.Environment);
        var database = await diagnosticsService.CheckDatabaseAsync(cancellationToken);
        var readiness = database.IsReachable
            ? await diagnosticsService.GetReadinessAsync(cancellationToken)
            : new ReadinessResponse
            {
                Ready = false,
                DatabaseReady = false,
                FredConfigured = configurationSnapshot.FredApiKeyConfigured && configurationSnapshot.FredBaseUrlEffectiveConfigured,
                Warnings = [$"Database is unavailable: {database.Message}"]
            };

        var warnings = new List<string>(readiness.Warnings);
        var openQuestions = new List<string>();

        DashboardSnapshot dashboard = DashboardSnapshot.Empty;
        IReadOnlyList<ModelSnapshotTradeCandidate> tradeCandidates = [];
        ModelSnapshotImportStatus? latestImport = null;
        ModelSnapshotImportStatus? latestSuccessfulImport = null;
        IReadOnlyList<ModelSnapshotImportStatus> latestImportsByDataSource = [];
        ModelSnapshotScoringStatus? latestScoring = null;
        ModelSnapshotScoringVisibility scoringVisibility = ModelSnapshotScoringVisibility.Empty;

        if (database.IsReachable)
        {
            dashboard = await factorScoringService.GetLatestSnapshotAsync();

            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var importRuns = await db.DataImportRuns
                .AsNoTracking()
                .Include(run => run.DataSource)
                .OrderByDescending(run => run.StartedAtUtc)
                .ThenByDescending(run => run.Id)
                .Select(run => new ModelSnapshotImportStatus(
                    run.DataSource == null ? "Unknown" : run.DataSource.Name,
                    run.StartedAtUtc,
                    run.FinishedAtUtc,
                    run.Status,
                    run.RowsRead,
                    run.RowsInserted,
                    run.RowsUpdated,
                    string.IsNullOrWhiteSpace(run.ErrorMessage) ? null : "Import recorded an error; review server logs or the imports page for details.",
                    run.Notes))
                .ToListAsync(cancellationToken);

            latestImport = importRuns.FirstOrDefault();
            latestSuccessfulImport = importRuns.FirstOrDefault(run => IsSuccessfulImportStatus(run.Status));
            latestImportsByDataSource = importRuns
                .GroupBy(run => run.DataSource, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderBy(run => run.DataSource)
                .ToList();

            var scoreDates = await db.FactorScores
                .AsNoTracking()
                .Select(score => new
                {
                    score.DataMode,
                    score.ScoreDate
                })
                .ToListAsync(cancellationToken);

            var latestScoreDatesByDataMode = scoreDates
                .GroupBy(score => score.DataMode, StringComparer.OrdinalIgnoreCase)
                .Select(group => new ModelSnapshotScoreDateByDataMode(
                    group.Key,
                    group.Max(score => score.ScoreDate),
                    group.Count()))
                .OrderBy(score => score.DataMode)
                .ToList();

            var latestSampleScoreDate = latestScoreDatesByDataMode
                .FirstOrDefault(score => IsDataMode(score.DataMode, "Sample"))
                ?.LatestScoreDate;
            var latestImportedManualScoreDate = latestScoreDatesByDataMode
                .FirstOrDefault(score => IsDataMode(score.DataMode, "ImportedManual"))
                ?.LatestScoreDate;

            scoringVisibility = new ModelSnapshotScoringVisibility(
                latestScoreDatesByDataMode,
                latestSampleScoreDate,
                latestImportedManualScoreDate,
                dashboard.DataMode,
                dashboard.AsOfDate == DateOnly.MinValue ? null : dashboard.AsOfDate);

            latestScoring = dashboard.AsOfDate == DateOnly.MinValue
                ? null
                : new ModelSnapshotScoringStatus(
                    dashboard.AsOfDate,
                    dashboard.DataMode,
                    dashboard.ScoringModelVersion,
                    dashboard.FactorScores.Count,
                    dashboard.FactorScores
                        .Select(score => score.CalculatedAtUtc)
                        .Where(value => value.HasValue)
                        .DefaultIfEmpty()
                        .Max());

            tradeCandidates = await db.TradeIdeas
                .AsNoTracking()
                .OrderByDescending(idea => idea.IdeaDate)
                .ThenByDescending(idea => idea.Id)
                .Take(20)
                .Select(idea => new ModelSnapshotTradeCandidate(
                    idea.Id,
                    idea.IdeaDate,
                    idea.Title,
                    idea.Thesis,
                    idea.Instrument,
                    idea.Status,
                    idea.EntryTrigger,
                    idea.Invalidation,
                    idea.Catalyst,
                    idea.RiskNotes,
                    idea.MaxLoss,
                    idea.TimeHorizon))
                .ToListAsync(cancellationToken);
        }

        var factorScores = dashboard.FactorScores
            .OrderBy(score => score.MacroFactor == null ? score.MacroFactorId.ToString() : score.MacroFactor.Name)
            .Select(ToFactorSnapshot)
            .ToList();

        var staleWarnings = factorScores
            .Where(score => IsStale(score.DataQualityStatus))
            .Select(score => $"{score.Name} data quality is {score.DataQualityStatus}.");
        warnings.AddRange(staleWarnings);

        if (dashboard.AsOfDate == DateOnly.MinValue)
        {
            warnings.Add("No factor scores are available yet.");
        }

        if (latestImport is null)
        {
            warnings.Add("No import run has been recorded yet.");
        }

        if (latestScoring is null)
        {
            warnings.Add("No scoring run is available in the current database.");
        }

        if (!factorScores.Any(score => score.WindowSlope.HasValue || score.WindowAcceleration.HasValue))
        {
            openQuestions.Add("Factor slope/acceleration is not yet available for the selected scoring set.");
        }

        if (dashboard.DataMode.Equals("Sample", StringComparison.OrdinalIgnoreCase))
        {
            openQuestions.Add("Dashboard is using sample scores; run imports and manual scoring before treating the snapshot as current data.");
        }

        var operationalState = BuildOperationalState(
            configurationSnapshot,
            database,
            readiness,
            dashboard.DataMode,
            latestImport,
            latestSuccessfulImport,
            scoringVisibility.LatestImportedManualScoreDate);

        return new ModelSnapshotResponse(
            DateTimeOffset.UtcNow,
            deployment,
            new ModelSnapshotDataStatus(
                configurationSnapshot.Environment,
                configurationSnapshot.DatabaseProvider,
                configurationSnapshot.DatabaseProviderConfigured,
                configurationSnapshot.DatabaseConnectionConfigured,
                database.IsReachable,
                database.Message,
                configurationSnapshot.FredApiKeyConfigured,
                configurationSnapshot.FredBaseUrlEffectiveConfigured,
                readiness.Ready,
                readiness.FredConfigured,
                readiness.ActiveFredSeriesCount),
            dashboard.AsOfDate == DateOnly.MinValue ? null : dashboard.AsOfDate,
            dashboard.CompositeScore,
            dashboard.CompositeRegimeLabel,
            dashboard.PrimaryMacroInterpretation,
            dashboard.DataMode,
            dashboard.ScoringModelVersion,
            dashboard.CategoryScores
                .Select(score => new ModelSnapshotCategoryScore(score.Category, score.Score))
                .ToList(),
            factorScores,
            dashboard.MacroInterpretations
                .Select(interpretation => new ModelSnapshotInterpretation(
                    interpretation.Name,
                    interpretation.Score,
                    interpretation.Reading,
                    interpretation.SupportingFactors,
                    interpretation.FactorContributions,
                    interpretation.Explanation))
                .ToList(),
            latestImport,
            latestScoring,
            latestSuccessfulImport,
            latestImportsByDataSource,
            scoringVisibility,
            operationalState,
            tradeCandidates,
            warnings.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(warning => warning).ToList(),
            openQuestions.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(question => question).ToList());
    }

    private static ModelSnapshotOperationalState BuildOperationalState(
        AppConfigurationDiagnosticsSnapshot configurationSnapshot,
        DatabaseReachabilityResult database,
        ReadinessResponse readiness,
        string dataMode,
        ModelSnapshotImportStatus? latestImport,
        ModelSnapshotImportStatus? latestSuccessfulImport,
        DateOnly? latestImportedManualScoreDate)
    {
        var isUsingSampleData = IsDataMode(dataMode, "Sample");
        var isUsingImportedManualData = IsDataMode(dataMode, "ImportedManual");
        var blockingReasons = new List<string>();
        var nonBlockingWarnings = new List<string>();
        var nextActions = new List<string>();

        if (!database.IsReachable)
        {
            blockingReasons.Add($"Database is unavailable: {database.Message}");
            nextActions.Add("Fix the configured database connection and re-check /ready.");
        }

        if (isUsingSampleData)
        {
            blockingReasons.Add("DataMode is Sample, so the snapshot is not using production imported/manual-scored data.");
            nextActions.Add("Run imports and manual scoring until the dashboard-selected score mode is ImportedManual.");
        }

        if (!configurationSnapshot.FredApiKeyConfigured)
        {
            blockingReasons.Add("Fred:ApiKey is missing; FRED imports are not configured.");
            nextActions.Add("Configure Fred__ApiKey in Render environment variables only.");
        }

        if (latestImport is null)
        {
            blockingReasons.Add("No import run has been recorded yet.");
            nextActions.Add("Run the import workflow and confirm an import run is recorded.");
        }
        else if (latestSuccessfulImport is null)
        {
            blockingReasons.Add("No successful import run has been recorded yet.");
            nextActions.Add("Review the imports page and server logs, then rerun imports until one completes successfully.");
        }

        if (latestImportedManualScoreDate is null)
        {
            blockingReasons.Add("No ImportedManual scores have been recorded yet.");
            nextActions.Add("Run manual scoring after imports complete successfully.");
        }

        if (configurationSnapshot.Environment.Equals("Production", StringComparison.OrdinalIgnoreCase)
            && configurationSnapshot.DatabaseProvider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            nonBlockingWarnings.Add("Current storage mode / limitation: Production is using SQLite. This is acceptable for the current Render Free early prototype, but Postgres/Supabase remains the future durability upgrade.");
            nextActions.Add("Keep the manual import/scoring loop on SQLite for the current Render Free prototype; plan Supabase/Postgres as a future durability upgrade.");
        }

        foreach (var warning in readiness.Warnings)
        {
            if (!blockingReasons.Contains(warning, StringComparer.OrdinalIgnoreCase))
            {
                blockingReasons.Add(warning);
            }
        }

        var productionDataReady = database.IsReachable
            && isUsingImportedManualData
            && blockingReasons.Count == 0;

        if (productionDataReady)
        {
            nextActions.Add("Continue scheduled imports and manual scoring reviews; no readiness blockers are currently detected.");
        }

        return new ModelSnapshotOperationalState(
            dataMode,
            isUsingSampleData,
            isUsingImportedManualData,
            productionDataReady,
            blockingReasons.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(reason => reason).ToList(),
            nextActions.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(action => action).ToList())
        {
            NonBlockingWarnings = nonBlockingWarnings
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(warning => warning)
                .ToList()
        };
    }

    private ModelSnapshotDeploymentMetadata GetDeploymentMetadata(string environment)
    {
        return new ModelSnapshotDeploymentMetadata(
            environment,
            FirstConfiguredValue("Render:ServiceName", "RENDER_SERVICE_NAME"),
            FirstConfiguredValue("Render:ServiceId", "RENDER_SERVICE_ID"),
            FirstConfiguredValue("Render:ExternalHostname", "RENDER_EXTERNAL_HOSTNAME"),
            FirstConfiguredValue("Render:GitBranch", "RENDER_GIT_BRANCH", "GIT_BRANCH"),
            FirstConfiguredValue("Render:GitCommit", "RENDER_GIT_COMMIT", "GIT_COMMIT", "SOURCE_VERSION"),
            FirstConfiguredValue("Render:GitRepository", "RENDER_GIT_REPO_SLUG", "RENDER_GIT_REPOSITORY", "GIT_REPOSITORY"),
            FirstConfiguredValue("Render:DeployId", "RENDER_DEPLOY_ID"),
            FirstConfiguredValue("Render:InstanceId", "RENDER_INSTANCE_ID"));
    }

    private string? FirstConfiguredValue(params string[] keys)
    {
        foreach (var key in keys)
        {
            var value = configuration[key];
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static ModelSnapshotFactorScore ToFactorSnapshot(MacroRegimeFactorMonitor.Domain.FactorScore score)
    {
        var factorName = score.MacroFactor?.Name ?? $"Factor {score.MacroFactorId}";

        return new ModelSnapshotFactorScore(
            factorName,
            score.MacroFactor?.Category ?? "Uncategorized",
            score.RawScore,
            score.WeightedScore,
            FactorScoreCalculator.CalculatePressureContribution(score),
            score.RegimeImpact,
            score.DataMode,
            score.ScoringModelVersion,
            score.SourceObservationDate,
            score.PreviousObservationDate,
            score.SourceObservationValue,
            score.PreviousObservationValue,
            score.ObservationChange,
            score.ObservationChangePercent,
            score.DaysSinceSourceObservation,
            score.DataQualityStatus,
            score.DataQualityNotes,
            score.WindowObservationCount,
            score.WindowStartDate,
            score.WindowEndDate,
            score.WindowChange,
            score.WindowChangePercent,
            score.WindowSlope,
            score.WindowAcceleration,
            score.ScoringConfidence,
            score.ScoringConfidenceNotes,
            score.CalculatedAtUtc,
            score.CalculationNotes);
    }

    private static bool IsStale(string? dataQualityStatus)
    {
        return dataQualityStatus is not null
            && dataQualityStatus.Contains("stale", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSuccessfulImportStatus(string status)
    {
        return status.Equals("Completed", StringComparison.OrdinalIgnoreCase)
            || status.Equals("Succeeded", StringComparison.OrdinalIgnoreCase)
            || status.Equals("Success", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDataMode(string? value, string expected)
    {
        return value is not null && value.Equals(expected, StringComparison.OrdinalIgnoreCase);
    }
}

public sealed record ModelSnapshotResponse(
    DateTimeOffset AsOfUtc,
    ModelSnapshotDeploymentMetadata Deployment,
    ModelSnapshotDataStatus DataStatus,
    DateOnly? LatestScoreDate,
    decimal CompositeScore,
    string CompositeRegimeLabel,
    string PrimaryMacroInterpretation,
    string DataMode,
    string ScoringModelVersion,
    IReadOnlyList<ModelSnapshotCategoryScore> CategoryScores,
    IReadOnlyList<ModelSnapshotFactorScore> FactorScores,
    IReadOnlyList<ModelSnapshotInterpretation> DerivedInterpretations,
    ModelSnapshotImportStatus? LatestImportStatus,
    ModelSnapshotScoringStatus? LatestScoringStatus,
    ModelSnapshotImportStatus? LatestSuccessfulImportStatus,
    IReadOnlyList<ModelSnapshotImportStatus> LatestImportsByDataSource,
    ModelSnapshotScoringVisibility ScoringVisibility,
    ModelSnapshotOperationalState OperationalState,
    IReadOnlyList<ModelSnapshotTradeCandidate> TradeCandidates,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> OpenQuestions);

public sealed record ModelSnapshotDeploymentMetadata(
    string Environment,
    string? ServiceName,
    string? ServiceId,
    string? ExternalHostname,
    string? GitBranch,
    string? GitCommit,
    string? GitRepository,
    string? DeployId,
    string? InstanceId);

public sealed record ModelSnapshotDataStatus(
    string Environment,
    string DatabaseProvider,
    bool DatabaseProviderConfigured,
    bool DatabaseConnectionConfigured,
    bool DatabaseReachable,
    string DatabaseMessage,
    bool FredApiKeyConfigured,
    bool FredBaseUrlConfigured,
    bool Ready,
    bool FredReady,
    int ActiveFredSeriesCount);

public sealed record ModelSnapshotOperationalState(
    string DataMode,
    bool IsUsingSampleData,
    bool IsUsingImportedManualData,
    bool ProductionDataReady,
    IReadOnlyList<string> BlockingReasons,
    IReadOnlyList<string> NextActions)
{
    public IReadOnlyList<string> NonBlockingWarnings { get; init; } = [];
}

public sealed record ModelSnapshotScoringVisibility(
    IReadOnlyList<ModelSnapshotScoreDateByDataMode> LatestScoreDateByDataMode,
    DateOnly? LatestSampleScoreDate,
    DateOnly? LatestImportedManualScoreDate,
    string SelectedScoreMode,
    DateOnly? SelectedScoreDate)
{
    public static ModelSnapshotScoringVisibility Empty { get; } = new([], null, null, "Unknown", null);
}

public sealed record ModelSnapshotScoreDateByDataMode(
    string DataMode,
    DateOnly LatestScoreDate,
    int FactorScoreCount);

public sealed record ModelSnapshotCategoryScore(string Category, decimal Score);

public sealed record ModelSnapshotFactorScore(
    string Name,
    string Category,
    decimal RawScore,
    decimal WeightedScore,
    decimal PressureContribution,
    string RegimeImpact,
    string DataMode,
    string? ScoringModelVersion,
    DateOnly? SourceObservationDate,
    DateOnly? PreviousObservationDate,
    decimal? SourceObservationValue,
    decimal? PreviousObservationValue,
    decimal? ObservationChange,
    decimal? ObservationChangePercent,
    int? DaysSinceSourceObservation,
    string? DataQualityStatus,
    string? DataQualityNotes,
    int? WindowObservationCount,
    DateOnly? WindowStartDate,
    DateOnly? WindowEndDate,
    decimal? WindowChange,
    decimal? WindowChangePercent,
    decimal? WindowSlope,
    decimal? WindowAcceleration,
    string? ScoringConfidence,
    string? ScoringConfidenceNotes,
    DateTime? CalculatedAtUtc,
    string? CalculationNotes);

public sealed record ModelSnapshotInterpretation(
    string Name,
    decimal Score,
    string Reading,
    string SupportingFactors,
    string FactorContributions,
    string Explanation);

public sealed record ModelSnapshotImportStatus(
    string DataSource,
    DateTime StartedAtUtc,
    DateTime? FinishedAtUtc,
    string Status,
    int RowsRead,
    int RowsInserted,
    int RowsUpdated,
    string? ErrorSummary,
    string Notes);

public sealed record ModelSnapshotScoringStatus(
    DateOnly ScoreDate,
    string DataMode,
    string ScoringModelVersion,
    int FactorScoreCount,
    DateTime? LatestCalculatedAtUtc);

public sealed record ModelSnapshotTradeCandidate(
    int Id,
    DateOnly IdeaDate,
    string Title,
    string Thesis,
    string Instrument,
    string Status,
    string EntryTrigger,
    string Invalidation,
    string Catalyst,
    string RiskNotes,
    string MaxLoss,
    string TimeHorizon);
