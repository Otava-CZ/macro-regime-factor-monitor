using MacroRegimeFactorMonitor.Domain;

namespace MacroRegimeFactorMonitor.Services.Imports;

public sealed class BlsDataSourceClient : IDataSourceClient
{
    public string SourceName => "BLS";

    public Task<IReadOnlyList<ImportObservationDto>> FetchObservationsAsync(
        ExternalSeries externalSeries,
        DateOnly? fromDate,
        DateOnly? toDate,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("BLS import client is not implemented yet. This PR only adds the import architecture skeleton.");
    }
}
