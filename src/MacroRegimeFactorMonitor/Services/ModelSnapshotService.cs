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
        ModelSnapshotScoringStatus? latestScoring = null;

        if (database.IsReachable)
        {
            dashboard = await factorScoringService.GetLatestSnapshotAsync();

            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            latestImport = await db.DataImportRuns
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
                .FirstOrDefaultAsync(cancellationToken);

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
            tradeCandidates,
            warnings.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(warning => warning).ToList(),
            openQuestions.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(question => question).ToList());
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
