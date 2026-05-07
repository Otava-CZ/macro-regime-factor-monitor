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
        >= 1.0m => "Strong positive factor move",
        >= 0.25m => "Positive factor move",
        <= -1.0m => "Strong negative factor move",
        <= -0.25m => "Negative factor move",
        _ => "Balanced"
    };

    public static string ClassifyRegime(decimal compositeScore) => compositeScore switch
    {
        >= 1.5m => "Expansion / Risk-On",
        >= 0.4m => "Constructive Growth",
        <= -1.5m => "Contraction / Risk-Off",
        <= -0.4m => "Defensive Slowdown",
        _ => "Neutral / Transition"
    };
}
