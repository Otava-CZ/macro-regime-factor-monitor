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

    public static string ClassifyImpact(decimal rawScore, string factorName = "") =>
        ClassifyPressureImpact(rawScore, factorName);

    public static decimal CalculatePressureContribution(decimal weightedScore, string? factorName)
    {
        var contribution = IsMarketComplacency(factorName)
            ? weightedScore
            : -weightedScore;

        return Math.Round(contribution, 2);
    }

    public static string ClassifyPressureImpact(decimal rawScore, string? factorName = null)
    {
        if (IsMarketComplacency(factorName))
        {
            return rawScore switch
            {
                >= 0.25m => "Complacency pressure",
                <= -0.25m => "Relief",
                _ => "Balanced"
            };
        }

        return rawScore switch
        {
            <= -1.0m => "Pressure rising",
            <= -0.25m => "Mild pressure",
            >= 0.25m => "Relief",
            _ => "Balanced"
        };
    }

    public static string ExplainPressureImpact(decimal rawScore, string? factorName = null)
    {
        if (IsMarketComplacency(factorName))
        {
            return rawScore switch
            {
                >= 0.25m => "Low volatility/complacency can underprice macro risk, so it is treated as mispricing pressure rather than a bullish signal.",
                <= -0.25m => "Volatility is no longer complacent, reducing the specific market complacency/mispricing pressure reading.",
                _ => "The complacency factor is close to baseline, so it is not adding clear pressure or relief."
            };
        }

        return rawScore switch
        {
            <= -1.0m => "This factor is materially away from baseline in the pressure direction.",
            <= -0.25m => "This factor is modestly away from baseline in the pressure direction.",
            >= 0.25m => "This factor is away from baseline in the relief direction.",
            _ => "This factor is close to baseline and is treated as balanced."
        };
    }

    public static string ClassifyRegime(decimal compositeScore) => compositeScore switch
    {
        >= 1.5m => "Expansion / Risk-On",
        >= 0.4m => "Constructive Growth",
        <= -1.5m => "Contraction / Risk-Off",
        <= -0.4m => "Defensive Slowdown",
        _ => "Neutral / Transition"
    };

    private static bool IsMarketComplacency(string? factorName) =>
        string.Equals(factorName, "Market Complacency", StringComparison.OrdinalIgnoreCase);
}
