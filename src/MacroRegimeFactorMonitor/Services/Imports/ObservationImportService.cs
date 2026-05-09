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

            var warnings = new List<string>();
            var rowsInserted = 0;
            var rowsUpdated = 0;
            var skippedExistingCount = 0;
            var skippedInvalidCount = 0;
            var skippedDuplicateFetchedDateCount = 0;
            var now = DateTime.UtcNow;
            var processedObservationDates = new HashSet<DateOnly>();

            var observationDates = observations
                .Select(observation => observation.ObservationDate)
                .Distinct()
                .ToList();

            var existingObservations = await dbContext.IndicatorObservations
                .Where(observation =>
                    observation.IndicatorId == externalSeries.IndicatorId
                    && observationDates.Contains(observation.ObservationDate))
                .ToDictionaryAsync(observation => observation.ObservationDate, cancellationToken);

            foreach (var observation in observations)
            {
                if (!IsValidObservation(observation, externalSeries))
                {
                    skippedInvalidCount++;
                    continue;
                }

                if (!processedObservationDates.Add(observation.ObservationDate))
                {
                    skippedDuplicateFetchedDateCount++;
                    continue;
                }

                if (!existingObservations.TryGetValue(observation.ObservationDate, out var existingObservation))
                {
                    var indicatorObservation = new IndicatorObservation
                    {
                        IndicatorId = externalSeries.IndicatorId,
                        ObservationDate = observation.ObservationDate,
                        Value = observation.Value,
                        Source = observation.Source,
                        ExternalSeriesId = externalSeries.Id,
                        DataImportRunId = importRun.Id,
                        SourceReleaseDate = observation.SourceReleaseDate,
                        VintageDate = observation.VintageDate,
                        CreatedAtUtc = now,
                        UpdatedAtUtc = now
                    };

                    dbContext.IndicatorObservations.Add(indicatorObservation);
                    existingObservations[observation.ObservationDate] = indicatorObservation;
                    rowsInserted++;
                    continue;
                }

                if (!request.ForceRefresh)
                {
                    skippedExistingCount++;
                    continue;
                }

                existingObservation.Value = observation.Value;
                existingObservation.Source = observation.Source;
                existingObservation.ExternalSeriesId = externalSeries.Id;
                existingObservation.DataImportRunId = importRun.Id;
                existingObservation.SourceReleaseDate = observation.SourceReleaseDate;
                existingObservation.VintageDate = observation.VintageDate;
                existingObservation.UpdatedAtUtc = now;
                rowsUpdated++;
            }

            var rowsSkipped = skippedExistingCount + skippedInvalidCount + skippedDuplicateFetchedDateCount;

            if (skippedExistingCount > 0)
            {
                warnings.Add($"{skippedExistingCount} existing observations were skipped because ForceRefresh is false.");
            }

            if (skippedInvalidCount > 0)
            {
                warnings.Add($"{skippedInvalidCount} invalid observations were skipped.");
            }

            if (skippedDuplicateFetchedDateCount > 0)
            {
                warnings.Add($"{skippedDuplicateFetchedDateCount} duplicate fetched observations were skipped.");
            }

            if (rowsInserted + rowsUpdated == 0)
            {
                warnings.Add("No observations were inserted or updated.");
            }

            importRun.Status = "Completed";
            importRun.FinishedAtUtc = now;
            importRun.RowsRead = observations.Count;
            importRun.RowsInserted = rowsInserted;
            importRun.RowsUpdated = rowsUpdated;
            importRun.ErrorMessage = string.Empty;
            importRun.Notes = "Import completed. Observations were inserted/updated into IndicatorObservations. Dashboard scoring remains unchanged.";
            externalSeries.LastSuccessfulImportUtc = importRun.FinishedAtUtc;
            externalSeries.UpdatedAtUtc = importRun.FinishedAtUtc;

            await dbContext.SaveChangesAsync(cancellationToken);

            return new ImportSeriesResult
            {
                RowsRead = observations.Count,
                RowsInserted = rowsInserted,
                RowsUpdated = rowsUpdated,
                RowsSkipped = rowsSkipped,
                Status = "Completed",
                Warnings = warnings
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

    private static bool IsValidObservation(
        ImportObservationDto observation,
        ExternalSeries externalSeries)
    {
        if (observation.ObservationDate == default || observation.ObservationDate == DateOnly.MinValue)
        {
            return false;
        }

        if (!string.Equals(observation.ExternalSeriesId, externalSeries.ExternalSeriesId, StringComparison.Ordinal))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(observation.Source))
        {
            return false;
        }

        return true;
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
