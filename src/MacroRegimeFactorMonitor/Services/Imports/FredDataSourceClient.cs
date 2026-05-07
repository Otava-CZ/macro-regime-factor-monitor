using MacroRegimeFactorMonitor.Domain;

namespace MacroRegimeFactorMonitor.Services.Imports;

public sealed class FredDataSourceClient : IDataSourceClient
{
    public string SourceName => "FRED";

    public Task<IReadOnlyList<ImportObservationDto>> FetchObservationsAsync(
        ExternalSeries externalSeries,
        DateOnly? fromDate,
        DateOnly? toDate,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("FRED import client is not implemented yet. This PR only adds the import architecture skeleton.");
    }
}
