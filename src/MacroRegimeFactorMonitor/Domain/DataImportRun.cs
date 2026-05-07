namespace MacroRegimeFactorMonitor.Domain;

public sealed class DataImportRun
{
    public int Id { get; set; }
    public int DataSourceId { get; set; }
    public DataSource? DataSource { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public DateTime? FinishedAtUtc { get; set; }
    public required string Status { get; set; }
    public int RowsRead { get; set; }
    public int RowsInserted { get; set; }
    public int RowsUpdated { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;

    public List<IndicatorObservation> Observations { get; set; } = [];
}
