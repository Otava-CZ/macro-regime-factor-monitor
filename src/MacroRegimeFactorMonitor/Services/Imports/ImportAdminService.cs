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

        return await query
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

    public Task<ImportSeriesResult> RunImportAsync(
        ImportSeriesRequest request,
        CancellationToken cancellationToken = default) =>
        observationImportService.ImportSeriesAsync(request, cancellationToken);
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
