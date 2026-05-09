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
    public DateOnly? SourceObservationDate { get; set; }
    public DateOnly? PreviousObservationDate { get; set; }
    public decimal? SourceObservationValue { get; set; }
    public decimal? PreviousObservationValue { get; set; }
    public decimal? ObservationChange { get; set; }
    public decimal? ObservationChangePercent { get; set; }
    public int? DaysSinceSourceObservation { get; set; }
    public string? DataQualityStatus { get; set; }
    public string? DataQualityNotes { get; set; }
    public DateTime? CalculatedAtUtc { get; set; }
    public string? CalculationNotes { get; set; }
}
