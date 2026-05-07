namespace MacroRegimeFactorMonitor.Domain;

public sealed class IndicatorObservation
{
    public int Id { get; set; }
    public int IndicatorId { get; set; }
    public Indicator? Indicator { get; set; }
    public DateOnly ObservationDate { get; set; }
    public decimal Value { get; set; }
    public int? ExternalSeriesId { get; set; }
    public ExternalSeries? ExternalSeries { get; set; }
    public int? DataImportRunId { get; set; }
    public DataImportRun? DataImportRun { get; set; }
    public string Source { get; set; } = string.Empty;
    public DateOnly? SourceReleaseDate { get; set; }
    public DateOnly? VintageDate { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
    public string Notes { get; set; } = string.Empty;
}
