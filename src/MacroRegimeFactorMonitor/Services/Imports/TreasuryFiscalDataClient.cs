using MacroRegimeFactorMonitor.Domain;

namespace MacroRegimeFactorMonitor.Services.Imports;

public sealed class TreasuryFiscalDataClient : IDataSourceClient
{
    public string SourceName => "Treasury Fiscal Data";

    public Task<IReadOnlyList<ImportObservationDto>> FetchObservationsAsync(
        ExternalSeries externalSeries,
        DateOnly? fromDate,
        DateOnly? toDate,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Treasury Fiscal Data import client is not implemented yet. This PR only adds the import architecture skeleton.");
    }
}
