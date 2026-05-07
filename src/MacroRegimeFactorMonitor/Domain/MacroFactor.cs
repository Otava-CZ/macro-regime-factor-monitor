namespace MacroRegimeFactorMonitor.Domain;

public sealed class MacroFactor
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Category { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Weight { get; set; }
    public bool HigherIsRiskOn { get; set; }

    public List<Indicator> Indicators { get; set; } = [];
    public List<FactorScore> Scores { get; set; } = [];
}
