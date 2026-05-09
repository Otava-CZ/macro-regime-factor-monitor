namespace MacroRegimeFactorMonitor.Domain;

public sealed class FactorScore
{
    public int Id { get; set; }
    public int MacroFactorId { get; set; }
    public MacroFactor? MacroFactor { get; set; }
    public DateOnly ScoreDate { get; set; }
    public decimal RawScore { get; set; }
    public decimal WeightedScore { get; set; }
    public required string RegimeImpact { get; set; }
    public string Notes { get; set; } = string.Empty;
    public string DataMode { get; set; } = "Sample";
    public string? ScoringModelVersion { get; set; } = "sample-v0";
    public int SourceObservationCount { get; set; }
    public DateTime? CalculatedAtUtc { get; set; }
    public string? CalculationNotes { get; set; }
}
