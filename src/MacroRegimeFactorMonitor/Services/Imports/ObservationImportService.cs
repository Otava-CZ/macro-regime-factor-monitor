using MacroRegimeFactorMonitor.Data;
using MacroRegimeFactorMonitor.Domain;
using Microsoft.EntityFrameworkCore;

namespace MacroRegimeFactorMonitor.Services.Imports;

public sealed class ObservationImportService(
    IDbContextFactory<MacroRegimeDbContext> dbContextFactory,
    IDataSourceClientFactory clientFactory) : IObservationImportService
{
    public async Task<ImportSeriesResult> ImportSeriesAsync(
        ImportSeriesRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var externalSeries = await dbContext.ExternalSeries
            .Include(series => series.DataSource)
            .Include(series => series.Indicator)
            .SingleOrDefaultAsync(series => series.Id == request.ExternalSeriesId, cancellationToken);

        if (externalSeries is null)
        {
            return FailedResult($"External series with id {request.ExternalSeriesId} was not found.");
        }

        if (externalSeries.DataSource is null)
        {
            return FailedResult($"External series with id {request.ExternalSeriesId} is not linked to a data source.");
        }

        var importRun = new DataImportRun
        {
            DataSourceId = externalSeries.DataSourceId,
            StartedAtUtc = DateTime.UtcNow,
            Status = "Started",
            Notes = $"Import skeleton run for external series {externalSeries.ExternalSeriesId}."
        };

        dbContext.DataImportRuns.Add(importRun);
        await dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            var client = clientFactory.GetClient(externalSeries.DataSource.Name);
            var observations = await client.FetchObservationsAsync(
                externalSeries,
                request.FromDate,
                request.ToDate,
                cancellationToken);

            importRun.Status = "Completed";
            importRun.FinishedAtUtc = DateTime.UtcNow;
            importRun.RowsRead = observations.Count;
            importRun.RowsInserted = 0;
            importRun.RowsUpdated = 0;
            importRun.Notes = "Import skeleton completed without persisting observations. Upsert, validation, and import-run observation linkage will be implemented in a later PR.";

            await dbContext.SaveChangesAsync(cancellationToken);

            return new ImportSeriesResult
            {
                RowsRead = observations.Count,
                RowsInserted = 0,
                RowsUpdated = 0,
                RowsSkipped = observations.Count,
                Status = "Completed",
                Warnings =
                [
                    "Observation persistence is not implemented yet. No observations were inserted or updated."
                ]
            };
        }
        catch (Exception exception)
        {
            importRun.Status = "Failed";
            importRun.FinishedAtUtc = DateTime.UtcNow;
            importRun.ErrorMessage = exception.Message;

            await dbContext.SaveChangesAsync(cancellationToken);

            return FailedResult(exception.Message);
        }
    }

    private static ImportSeriesResult FailedResult(string errorMessage)
    {
        return new ImportSeriesResult
        {
            Status = "Failed",
            ErrorMessage = errorMessage
        };
    }
}
