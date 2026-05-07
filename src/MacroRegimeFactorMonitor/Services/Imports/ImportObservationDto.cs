using MacroRegimeFactorMonitor.Domain;

namespace MacroRegimeFactorMonitor.Services.Imports;

public sealed class ImportObservationDto
{
    public DateOnly ObservationDate { get; set; }
    public decimal Value { get; set; }
    public DateOnly? SourceReleaseDate { get; set; }
    public DateOnly? VintageDate { get; set; }
    public required string Source { get; set; }
    public required string ExternalSeriesId { get; set; }
    public string Notes { get; set; } = string.Empty;
}

public sealed class ImportSeriesRequest
{
    public int ExternalSeriesId { get; set; }
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    public bool ForceRefresh { get; set; }
}

public sealed class ImportSeriesResult
{
    public int RowsRead { get; set; }
    public int RowsInserted { get; set; }
    public int RowsUpdated { get; set; }
    public int RowsSkipped { get; set; }
    public string Status { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public List<string> Warnings { get; set; } = [];
}

public interface IDataSourceClient
{
    string SourceName { get; }

    Task<IReadOnlyList<ImportObservationDto>> FetchObservationsAsync(
        ExternalSeries externalSeries,
        DateOnly? fromDate,
        DateOnly? toDate,
        CancellationToken cancellationToken = default);
}

public interface IDataSourceClientFactory
{
    IDataSourceClient GetClient(string sourceName);
}

public interface IObservationImportService
{
    Task<ImportSeriesResult> ImportSeriesAsync(
        ImportSeriesRequest request,
        CancellationToken cancellationToken = default);
}
