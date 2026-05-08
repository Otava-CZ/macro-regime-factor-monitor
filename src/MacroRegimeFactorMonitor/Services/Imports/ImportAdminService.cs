using MacroRegimeFactorMonitor.Data;
using Microsoft.EntityFrameworkCore;

namespace MacroRegimeFactorMonitor.Services.Imports;

public sealed class ImportAdminService(
    IDbContextFactory<MacroRegimeDbContext> dbContextFactory,
    IObservationImportService observationImportService)
{
    public async Task<List<ExternalSeriesAdminRow>> GetExternalSeriesAsync(
        bool includeInactive,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var query = dbContext.ExternalSeries
            .AsNoTracking()
            .Include(series => series.DataSource)
            .Include(series => series.Indicator)
            .AsQueryable();

        if (!includeInactive)
        {
            query = query.Where(series => series.IsActive);
        }

        var rows = await query
            .OrderBy(series => series.DataSource!.Name)
            .ThenBy(series => series.ExternalSeriesId)
            .Select(series => new ExternalSeriesAdminRow
            {
                Id = series.Id,
                DataSourceName = series.DataSource == null ? string.Empty : series.DataSource.Name,
                IndicatorName = series.Indicator == null ? string.Empty : series.Indicator.Name,
                ExternalSeriesId = series.ExternalSeriesId,
                Frequency = series.Frequency,
                Units = series.Units,
                Transform = series.Transform,
                IsActive = series.IsActive,
                LastSuccessfulImportUtc = series.LastSuccessfulImportUtc
            })
            .ToListAsync(cancellationToken);

        var seriesIds = rows.Select(row => row.Id).ToList();
        var latestObservationDates = await dbContext.IndicatorObservations
            .AsNoTracking()
            .Where(observation => observation.ExternalSeriesId.HasValue && seriesIds.Contains(observation.ExternalSeriesId.Value))
            .GroupBy(observation => observation.ExternalSeriesId!.Value)
            .Select(group => new
            {
                ExternalSeriesId = group.Key,
                LatestObservationDate = group.Max(observation => observation.ObservationDate)
            })
            .ToDictionaryAsync(row => row.ExternalSeriesId, row => row.LatestObservationDate, cancellationToken);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        foreach (var row in rows)
        {
            if (!latestObservationDates.TryGetValue(row.Id, out var latestObservationDate))
            {
                row.FreshnessStatus = FreshnessStatus.Missing;
                continue;
            }

            row.LatestObservationDate = latestObservationDate;
            row.DaysSinceLatestObservation = Math.Max(0, today.DayNumber - latestObservationDate.DayNumber);
            row.FreshnessStatus = row.DaysSinceLatestObservation <= GetFreshnessThresholdDays(row.Frequency)
                ? FreshnessStatus.Fresh
                : FreshnessStatus.Stale;
        }

        return rows;
    }

    public async Task<List<DataImportRunAdminRow>> GetRecentImportRunsAsync(
        int take = 20,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await dbContext.DataImportRuns
            .AsNoTracking()
            .Include(run => run.DataSource)
            .OrderByDescending(run => run.StartedAtUtc)
            .ThenByDescending(run => run.Id)
            .Take(take)
            .Select(run => new DataImportRunAdminRow
            {
                StartedAtUtc = run.StartedAtUtc,
                FinishedAtUtc = run.FinishedAtUtc,
                DataSourceName = run.DataSource == null ? string.Empty : run.DataSource.Name,
                Status = run.Status,
                RowsRead = run.RowsRead,
                RowsInserted = run.RowsInserted,
                RowsUpdated = run.RowsUpdated,
                ErrorMessage = run.ErrorMessage,
                Notes = run.Notes
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<List<ImportedObservationAdminRow>> GetRecentObservationsAsync(
        int? externalSeriesId = null,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var query = dbContext.IndicatorObservations
            .AsNoTracking()
            .Include(observation => observation.Indicator)
            .Include(observation => observation.ExternalSeries)
            .AsQueryable();

        if (externalSeriesId.HasValue)
        {
            query = query.Where(observation => observation.ExternalSeriesId == externalSeriesId.Value);
        }

        return await query
            .OrderByDescending(observation => observation.ObservationDate)
            .ThenByDescending(observation => observation.UpdatedAtUtc ?? observation.CreatedAtUtc)
            .ThenByDescending(observation => observation.Id)
            .Take(take)
            .Select(observation => new ImportedObservationAdminRow
            {
                ObservationDate = observation.ObservationDate,
                IndicatorName = observation.Indicator == null ? string.Empty : observation.Indicator.Name,
                ExternalSeriesCode = observation.ExternalSeries == null ? string.Empty : observation.ExternalSeries.ExternalSeriesId,
                Value = observation.Value,
                Source = observation.Source,
                DataImportRunId = observation.DataImportRunId,
                CreatedAtUtc = observation.CreatedAtUtc,
                UpdatedAtUtc = observation.UpdatedAtUtc
            })
            .ToListAsync(cancellationToken);
    }

    public Task<ImportSeriesResult> RunImportAsync(
        ImportSeriesRequest request,
        CancellationToken cancellationToken = default) =>
        observationImportService.ImportSeriesAsync(request, cancellationToken);

    public async Task<ImportBatchResult> RefreshAllActiveFredSeriesAsync(
        CancellationToken cancellationToken = default)
    {
        var batchResult = new ImportBatchResult
        {
            StartedAtUtc = DateTime.UtcNow,
            Status = "Started"
        };

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var fredSeries = await dbContext.ExternalSeries
            .AsNoTracking()
            .Include(series => series.DataSource)
            .Where(series => series.IsActive
                && series.DataSource != null
                && series.DataSource.Name == "FRED")
            .OrderBy(series => series.ExternalSeriesId)
            .Select(series => new FredRefreshSeriesRow
            {
                Id = series.Id,
                IndicatorId = series.IndicatorId,
                ExternalSeriesId = series.ExternalSeriesId,
                Frequency = series.Frequency
            })
            .ToListAsync(cancellationToken);

        var fredSeriesIds = fredSeries.Select(series => series.Id).ToList();
        var latestObservationDates = await dbContext.IndicatorObservations
            .AsNoTracking()
            .Where(observation => observation.ExternalSeriesId.HasValue
                && fredSeriesIds.Contains(observation.ExternalSeriesId.Value))
            .GroupBy(observation => new { observation.IndicatorId, ExternalSeriesId = observation.ExternalSeriesId!.Value })
            .Select(group => new
            {
                group.Key.IndicatorId,
                group.Key.ExternalSeriesId,
                LatestObservationDate = group.Max(observation => observation.ObservationDate)
            })
            .ToListAsync(cancellationToken);
        var latestObservationDateLookup = latestObservationDates.ToDictionary(
            row => (row.IndicatorId, row.ExternalSeriesId),
            row => row.LatestObservationDate);

        foreach (var series in fredSeries)
        {
            if (latestObservationDateLookup.TryGetValue((series.IndicatorId, series.Id), out var latestObservationDate))
            {
                series.LatestObservationDate = latestObservationDate;
            }
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        foreach (var series in fredSeries)
        {
            var fromDate = CalculateRefreshFromDate(series.Frequency, series.LatestObservationDate, today);
            var seriesResult = new ImportBatchSeriesResult
            {
                ExternalSeriesId = series.Id,
                ExternalSeriesCode = series.ExternalSeriesId,
                FromDate = fromDate,
                ToDate = today
            };
            batchResult.SeriesResults.Add(seriesResult);

            try
            {
                var importResult = await observationImportService.ImportSeriesAsync(new ImportSeriesRequest
                {
                    ExternalSeriesId = series.Id,
                    FromDate = fromDate,
                    ToDate = today,
                    ForceRefresh = true
                }, cancellationToken);

                seriesResult.Status = importResult.Status;
                seriesResult.RowsRead = importResult.RowsRead;
                seriesResult.RowsInserted = importResult.RowsInserted;
                seriesResult.RowsUpdated = importResult.RowsUpdated;
                seriesResult.RowsSkipped = importResult.RowsSkipped;
                seriesResult.ErrorMessage = importResult.ErrorMessage;
                seriesResult.Warnings = importResult.Warnings;
            }
            catch (Exception exception)
            {
                seriesResult.Status = "Failed";
                seriesResult.ErrorMessage = exception.Message;
            }
        }

        batchResult.FinishedAtUtc = DateTime.UtcNow;
        batchResult.Status = batchResult.SeriesFailed > 0 ? "CompletedWithFailures" : "Completed";
        return batchResult;
    }

    private static DateOnly CalculateRefreshFromDate(
        string frequency,
        DateOnly? latestObservationDate,
        DateOnly today)
    {
        if (!latestObservationDate.HasValue)
        {
            return today.AddYears(-2);
        }

        if (IsDaily(frequency))
        {
            return latestObservationDate.Value.AddDays(-7);
        }

        if (IsMonthly(frequency))
        {
            return latestObservationDate.Value.AddMonths(-6);
        }

        return latestObservationDate.Value.AddDays(-30);
    }

    private static int GetFreshnessThresholdDays(string frequency)
    {
        if (IsDaily(frequency))
        {
            return 5;
        }

        if (IsMonthly(frequency))
        {
            return 45;
        }

        return 14;
    }

    private static bool IsDaily(string frequency) =>
        string.Equals(frequency, "Daily", StringComparison.OrdinalIgnoreCase);

    private static bool IsMonthly(string frequency) =>
        string.Equals(frequency, "Monthly", StringComparison.OrdinalIgnoreCase);

    private sealed class FredRefreshSeriesRow
    {
        public int Id { get; set; }
        public int IndicatorId { get; set; }
        public string ExternalSeriesId { get; set; } = string.Empty;
        public string Frequency { get; set; } = string.Empty;
        public DateOnly? LatestObservationDate { get; set; }
    }
}

public sealed class ExternalSeriesAdminRow
{
    public int Id { get; set; }
    public string DataSourceName { get; set; } = string.Empty;
    public string IndicatorName { get; set; } = string.Empty;
    public string ExternalSeriesId { get; set; } = string.Empty;
    public string Frequency { get; set; } = string.Empty;
    public string Units { get; set; } = string.Empty;
    public string Transform { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime? LastSuccessfulImportUtc { get; set; }
    public DateOnly? LatestObservationDate { get; set; }
    public int? DaysSinceLatestObservation { get; set; }
    public string FreshnessStatus { get; set; } = MacroRegimeFactorMonitor.Services.Imports.FreshnessStatus.Missing;
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    public bool ForceRefresh { get; set; }
}

public sealed class DataImportRunAdminRow
{
    public DateTime StartedAtUtc { get; set; }
    public DateTime? FinishedAtUtc { get; set; }
    public string DataSourceName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int RowsRead { get; set; }
    public int RowsInserted { get; set; }
    public int RowsUpdated { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
}

public sealed class ImportedObservationAdminRow
{
    public DateOnly ObservationDate { get; set; }
    public string IndicatorName { get; set; } = string.Empty;
    public string ExternalSeriesCode { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public string Source { get; set; } = string.Empty;
    public int? DataImportRunId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}

public sealed class ImportBatchResult
{
    public DateTime StartedAtUtc { get; set; }
    public DateTime? FinishedAtUtc { get; set; }
    public string Status { get; set; } = string.Empty;
    public List<ImportBatchSeriesResult> SeriesResults { get; set; } = [];
    public int SeriesAttempted => SeriesResults.Count;
    public int SeriesCompleted => SeriesResults.Count(result => string.Equals(result.Status, "Completed", StringComparison.OrdinalIgnoreCase));
    public int SeriesFailed => SeriesResults.Count(result => !string.Equals(result.Status, "Completed", StringComparison.OrdinalIgnoreCase));
    public int TotalRowsRead => SeriesResults.Sum(result => result.RowsRead);
    public int TotalRowsInserted => SeriesResults.Sum(result => result.RowsInserted);
    public int TotalRowsUpdated => SeriesResults.Sum(result => result.RowsUpdated);
    public int TotalRowsSkipped => SeriesResults.Sum(result => result.RowsSkipped);
}

public sealed class ImportBatchSeriesResult
{
    public int ExternalSeriesId { get; set; }
    public string ExternalSeriesCode { get; set; } = string.Empty;
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    public string Status { get; set; } = "Started";
    public int RowsRead { get; set; }
    public int RowsInserted { get; set; }
    public int RowsUpdated { get; set; }
    public int RowsSkipped { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public List<string> Warnings { get; set; } = [];
}

public static class FreshnessStatus
{
    public const string Fresh = "Fresh";
    public const string Stale = "Stale";
    public const string Missing = "Missing";
}
