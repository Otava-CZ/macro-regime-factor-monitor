namespace MacroRegimeFactorMonitor.Domain;

public sealed class IndicatorObservation
{
    public int Id { get; set; }
    public int IndicatorId { get; set; }
    public Indicator? Indicator { get; set; }
    public DateOnly ObservationDate { get; set; }
    public decimal Value { get; set; }
    public string Notes { get; set; } = string.Empty;
}
