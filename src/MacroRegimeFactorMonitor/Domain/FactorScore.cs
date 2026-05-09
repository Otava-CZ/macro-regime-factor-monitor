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
    public int? WindowObservationCount { get; set; }
    public DateOnly? WindowStartDate { get; set; }
    public DateOnly? WindowEndDate { get; set; }
    public decimal? WindowMinValue { get; set; }
    public decimal? WindowMaxValue { get; set; }
    public decimal? WindowAverageValue { get; set; }
    public decimal? WindowFirstValue { get; set; }
    public decimal? WindowLastValue { get; set; }
    public decimal? WindowChange { get; set; }
    public decimal? WindowChangePercent { get; set; }
    public decimal? WindowSlope { get; set; }
    public decimal? WindowAcceleration { get; set; }
    public string? ScoringConfidence { get; set; }
    public string? ScoringConfidenceNotes { get; set; }
    public DateTime? CalculatedAtUtc { get; set; }
    public string? CalculationNotes { get; set; }
}
