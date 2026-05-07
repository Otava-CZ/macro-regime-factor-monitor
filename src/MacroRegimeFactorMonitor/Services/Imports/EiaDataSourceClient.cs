using MacroRegimeFactorMonitor.Domain;

namespace MacroRegimeFactorMonitor.Services.Imports;

public sealed class EiaDataSourceClient : IDataSourceClient
{
    public string SourceName => "EIA";

    public Task<IReadOnlyList<ImportObservationDto>> FetchObservationsAsync(
        ExternalSeries externalSeries,
        DateOnly? fromDate,
        DateOnly? toDate,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("EIA import client is not implemented yet. This PR only adds the import architecture skeleton.");
    }
}
