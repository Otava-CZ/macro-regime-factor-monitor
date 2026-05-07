namespace MacroRegimeFactorMonitor.Domain;

public sealed class Indicator
{
    public int Id { get; set; }
    public int MacroFactorId { get; set; }
    public MacroFactor? MacroFactor { get; set; }
    public required string Name { get; set; }
    public string Source { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal Baseline { get; set; }
    public decimal Volatility { get; set; }

    public List<IndicatorObservation> Observations { get; set; } = [];
}
