namespace MacroRegimeFactorMonitor.Domain;

public sealed class ExternalSeries
{
    public int Id { get; set; }
    public int IndicatorId { get; set; }
    public Indicator? Indicator { get; set; }
    public int DataSourceId { get; set; }
    public DataSource? DataSource { get; set; }
    public required string ExternalSeriesId { get; set; }
    public required string Endpoint { get; set; }
    public required string Frequency { get; set; }
    public required string Units { get; set; }
    public required string Transform { get; set; }
    public required string ObservationDateField { get; set; }
    public required string ValueField { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastSuccessfulImportUtc { get; set; }
    public string Notes { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }

    public List<IndicatorObservation> Observations { get; set; } = [];
}
