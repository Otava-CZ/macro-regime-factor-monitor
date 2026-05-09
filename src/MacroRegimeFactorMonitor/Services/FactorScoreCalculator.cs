using MacroRegimeFactorMonitor.Domain;

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

    public static string ClassifyImpact(decimal pressureContribution, string factorName = "") =>
        ClassifyPressureImpact(pressureContribution, factorName);

    public static decimal CalculatePressureContribution(FactorScore score)
    {
        var contribution = string.Equals(score.DataMode, ImportedObservationScoringService.ImportedManualDataMode, StringComparison.OrdinalIgnoreCase)
            ? score.WeightedScore
            : CalculatePressureContribution(score.WeightedScore, score.MacroFactor?.Name);

        return Math.Round(contribution, 2);
    }

    public static decimal CalculatePressureContribution(decimal weightedScore, string? factorName)
    {
        var contribution = IsMarketComplacency(factorName)
            ? weightedScore
            : -weightedScore;

        return Math.Round(contribution, 2);
    }

    public static string ClassifyPressureImpact(decimal pressureContribution, string? factorName = null)
    {
        if (IsMarketComplacency(factorName))
        {
            return pressureContribution switch
            {
                >= 0.05m => "Complacency pressure",
                >= 0.02m => "Mild complacency pressure",
                <= -0.02m => "Relief",
                _ => "Balanced"
            };
        }

        return pressureContribution switch
        {
            >= 0.15m => "Pressure rising",
            >= 0.05m => "Mild pressure",
            <= -0.05m => "Relief",
            _ => "Balanced"
        };
    }

    public static string ExplainPressureImpact(decimal pressureContribution, string? factorName = null)
    {
        if (IsMarketComplacency(factorName))
        {
            return pressureContribution switch
            {
                >= 0.02m => "Low volatility/complacency can underprice macro risk, so it is treated as mispricing pressure rather than a bullish signal.",
                <= -0.02m => "Volatility is no longer complacent, reducing the specific market complacency/mispricing pressure reading.",
                _ => "The complacency factor is close to baseline, so it is not adding clear pressure or relief."
            };
        }

        return pressureContribution switch
        {
            >= 0.15m => "This factor is materially adding macro pressure after weighting.",
            >= 0.05m => "This factor is modestly adding macro pressure after weighting.",
            <= -0.05m => "This factor is contributing macro pressure relief after weighting.",
            _ => "This factor is close to balanced after weighting."
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
