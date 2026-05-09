using MacroRegimeFactorMonitor.Data;
using MacroRegimeFactorMonitor.Domain;
using MacroRegimeFactorMonitor.Services.Imports;
using Microsoft.EntityFrameworkCore;

namespace MacroRegimeFactorMonitor.Services;

public sealed class OperationalWorkflowService(
    IDbContextFactory<MacroRegimeDbContext> dbContextFactory,
    ImportAdminService importAdminService,
    ImportedObservationScoringService importedObservationScoringService)
{
    public async Task<OperationalWorkflowStatus> GetWorkflowStatusAsync(CancellationToken cancellationToken = default)
    {
        var freshnessRows = await GetFreshnessRowsAsync(cancellationToken);
        var latestScores = await GetLatestImportedManualScoresAsync(cancellationToken);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var latestImportRunTime = await dbContext.DataImportRuns
            .AsNoTracking()
            .MaxAsync(run => (DateTime?)run.StartedAtUtc, cancellationToken);

        var latestSuccessfulFredImportTime = await dbContext.DataImportRuns
            .AsNoTracking()
            .Include(run => run.DataSource)
            .Where(run => run.DataSource != null
                && run.DataSource.Name == "FRED"
                && run.Status == "Completed")
            .MaxAsync(run => (DateTime?)(run.FinishedAtUtc ?? run.StartedAtUtc), cancellationToken);

        var latestScore = latestScores.FirstOrDefault();

        return new OperationalWorkflowStatus
        {
            LatestImportRunTimeUtc = latestImportRunTime,
            LatestSuccessfulFredImportTimeUtc = latestSuccessfulFredImportTime,
            ActiveFredSeriesCount = freshnessRows.Count,
            SeriesFreshCount = freshnessRows.Count(row => row.FreshnessStatus == FreshnessStatus.Fresh),
            SeriesStaleCount = freshnessRows.Count(row => row.FreshnessStatus == FreshnessStatus.Stale),
            SeriesMissingCount = freshnessRows.Count(row => row.FreshnessStatus == FreshnessStatus.Missing),
            LatestImportedManualScoreDate = latestScore?.ScoreDate,
            LatestImportedManualScoringModelVersion = latestScore?.ScoringModelVersion,
            LatestImportedManualFactorScoreCount = latestScores.Count,
            HighConfidenceCount = latestScores.Count(score => string.Equals(score.ScoringConfidence, "High", StringComparison.OrdinalIgnoreCase)),
            MediumConfidenceCount = latestScores.Count(score => string.Equals(score.ScoringConfidence, "Medium", StringComparison.OrdinalIgnoreCase)),
            LowConfidenceCount = latestScores.Count(score => string.Equals(score.ScoringConfidence, "Low", StringComparison.OrdinalIgnoreCase)),
            PlaceholderCount = latestScores.Count(score => string.Equals(score.DataQualityStatus, "Placeholder", StringComparison.OrdinalIgnoreCase)),
            MissingCount = latestScores.Count(score => string.Equals(score.DataQualityStatus, "Missing", StringComparison.OrdinalIgnoreCase) || string.Equals(score.ScoringConfidence, "Missing", StringComparison.OrdinalIgnoreCase))
        };
    }

    public async Task<List<ExternalSeriesAdminRow>> GetFreshnessRowsAsync(CancellationToken cancellationToken = default)
    {
        var rows = await importAdminService.GetExternalSeriesAsync(includeInactive: false, cancellationToken);
        return rows
            .Where(row => string.Equals(row.DataSourceName, "FRED", StringComparison.OrdinalIgnoreCase))
            .OrderBy(row => row.ExternalSeriesId)
            .ToList();
    }

    public Task<IReadOnlyList<FactorScore>> GetLatestImportedManualScoresAsync(CancellationToken cancellationToken = default) =>
        importedObservationScoringService.GetLatestImportedManualScoresAsync(cancellationToken);

    public Task<ImportBatchResult> RefreshAllFredAsync(CancellationToken cancellationToken = default) =>
        importAdminService.RefreshAllActiveFredSeriesAsync(cancellationToken);

    public Task<ImportedObservationScoringResult> RecalculateScoresAsync(
        DateOnly scoreDate,
        CancellationToken cancellationToken = default) =>
        importedObservationScoringService.RecalculateCurrentScoresAsync(scoreDate, cancellationToken);

    public async Task<OperationalWorkflowRunResult> RunFullManualWorkflowAsync(
        DateOnly scoreDate,
        CancellationToken cancellationToken = default)
    {
        var refreshResult = await RefreshAllFredAsync(cancellationToken);
        var scoringResult = await RecalculateScoresAsync(scoreDate, cancellationToken);
        var status = await GetWorkflowStatusAsync(cancellationToken);

        var warnings = new List<string>();
        if (refreshResult.SeriesFailed > 0)
        {
            warnings.Add("Scoring ran after refresh with one or more import failures. Review freshness and confidence before using dashboard.");
        }

        warnings.AddRange(scoringResult.Warnings);

        return new OperationalWorkflowRunResult
        {
            RefreshResult = refreshResult,
            ScoringResult = scoringResult,
            Status = status,
            Warnings = warnings
        };
    }
}

public sealed class OperationalWorkflowStatus
{
    public DateTime? LatestImportRunTimeUtc { get; set; }
    public DateTime? LatestSuccessfulFredImportTimeUtc { get; set; }
    public int ActiveFredSeriesCount { get; set; }
    public int SeriesFreshCount { get; set; }
    public int SeriesStaleCount { get; set; }
    public int SeriesMissingCount { get; set; }
    public DateOnly? LatestImportedManualScoreDate { get; set; }
    public string? LatestImportedManualScoringModelVersion { get; set; }
    public int LatestImportedManualFactorScoreCount { get; set; }
    public int HighConfidenceCount { get; set; }
    public int MediumConfidenceCount { get; set; }
    public int LowConfidenceCount { get; set; }
    public int PlaceholderCount { get; set; }
    public int MissingCount { get; set; }
}

public sealed class OperationalWorkflowRunResult
{
    public ImportBatchResult? RefreshResult { get; set; }
    public ImportedObservationScoringResult? ScoringResult { get; set; }
    public OperationalWorkflowStatus? Status { get; set; }
    public List<string> Warnings { get; set; } = [];
}
