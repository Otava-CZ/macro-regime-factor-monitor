namespace MacroRegimeFactorMonitor.Services;

public static class FactorScoreCalculator
{
    public static decimal CalculateRawScore(decimal value, decimal baseline, decimal volatility, bool higherIsRiskOn)
    {
        if (volatility <= 0)
        {
            throw new InvalidOperationException("Indicator volatility must be positive before scoring.");
        }

        var direction = higherIsRiskOn ? 1 : -1;
        return Math.Round(((value - baseline) / volatility) * direction, 2);
    }

    public static decimal CalculateWeightedScore(decimal rawScore, decimal weight) =>
        Math.Round(rawScore * weight, 2);

    public static string ClassifyImpact(decimal rawScore) => rawScore switch
    {
        >= 1.0m => "Macro support",
        >= 0.25m => "Mild macro support",
        <= -1.0m => "Acute macro pressure",
        <= -0.25m => "Mild macro pressure",
        _ => "Balanced signal"
    };
}
