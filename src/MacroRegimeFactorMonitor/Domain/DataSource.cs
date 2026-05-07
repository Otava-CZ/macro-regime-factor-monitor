namespace MacroRegimeFactorMonitor.Domain;

public sealed class DataSource
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string SourceType { get; set; }
    public required string BaseUrl { get; set; }
    public bool RequiresApiKey { get; set; }
    public bool IsActive { get; set; } = true;
    public string Notes { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }

    public List<ExternalSeries> ExternalSeries { get; set; } = [];
    public List<DataImportRun> ImportRuns { get; set; } = [];
}
