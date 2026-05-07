using MacroRegimeFactorMonitor.Domain;

namespace MacroRegimeFactorMonitor.Services;

public static class MacroPressureInterpreter
{
    private const string InflationPressure = "Inflation Pressure";
    private const string InflationBreadth = "Inflation Breadth";
    private const string EnergyShock = "Energy Shock";
    private const string GrowthStress = "Growth Stress";
    private const string FiscalTreasuryStress = "Fiscal/Treasury Stress";
    private const string MarketComplacency = "Market Complacency";

    private static readonly IReadOnlyDictionary<string, PressurePolarity> FactorPressurePolarities = new Dictionary<string, PressurePolarity>
    {
        [InflationPressure] = PressurePolarity.NegativeScoreIsPressure,
        [InflationBreadth] = PressurePolarity.NegativeScoreIsPressure,
        [EnergyShock] = PressurePolarity.NegativeScoreIsPressure,
        [GrowthStress] = PressurePolarity.NegativeScoreIsPressure,
        [FiscalTreasuryStress] = PressurePolarity.NegativeScoreIsPressure,
        [MarketComplacency] = PressurePolarity.PositiveScoreIsPressure
    };

    public static IReadOnlyList<MacroInterpretation> BuildInterpretations(IReadOnlyList<FactorScore> scores)
    {
        var scoreByFactor = scores
            .Where(score => score.MacroFactor is not null)
            .ToDictionary(score => score.MacroFactor!.Name, StringComparer.OrdinalIgnoreCase);

        return
        [
            BuildInterpretation(
                "inflation/stagflation pressure",
                [InflationPressure, InflationBreadth, EnergyShock],
                scoreByFactor,
                "Inflation/stagflation pressure reflects whether price, breadth, and energy factors are adding macro pressure rather than relief."),
            BuildInterpretation(
                "fiscal/Treasury stress",
                [FiscalTreasuryStress],
                scoreByFactor,
                "Fiscal/Treasury stress rises when the Treasury-market factor points to tighter or more fragile financing conditions."),
            BuildInterpretation(
                "hard-landing pressure",
                [GrowthStress],
                scoreByFactor,
                "Hard-landing pressure rises when the growth stress factor shows weaker activity momentum versus its baseline."),
            BuildInterpretation(
                "market complacency/mispricing",
                [MarketComplacency],
                scoreByFactor,
                "Market complacency/mispricing is building when the Market Complacency factor indicates volatility is below its baseline.")
        ];
    }

    public static decimal CalculatePressureContribution(FactorScore score)
    {
        var polarity = GetPolarity(score.MacroFactor?.Name);
        return polarity == PressurePolarity.PositiveScoreIsPressure
            ? score.WeightedScore
            : -score.WeightedScore;
    }

    public static string ClassifyFactorImpact(FactorScore score)
    {
        var polarity = GetPolarity(score.MacroFactor?.Name);
        var pressureSignal = polarity == PressurePolarity.PositiveScoreIsPressure
            ? score.RawScore
            : -score.RawScore;

        if (IsMarketComplacency(score.MacroFactor?.Name))
        {
            return pressureSignal switch
            {
                >= 0.25m => "Complacency pressure",
                <= -0.25m => "Relief",
                _ => "Balanced"
            };
        }

        return pressureSignal switch
        {
            >= 1.0m => "Pressure rising",
            >= 0.25m => "Mild pressure",
            <= -0.25m => "Relief",
            _ => "Balanced"
        };
    }

    private static MacroInterpretation BuildInterpretation(
        string name,
        IReadOnlyList<string> factorNames,
        IReadOnlyDictionary<string, FactorScore> scoreByFactor,
        string explanation)
    {
        var includedScores = factorNames
            .Where(scoreByFactor.ContainsKey)
            .Select(factorName => scoreByFactor[factorName])
            .ToList();
        var score = Math.Round(includedScores.Sum(CalculatePressureContribution), 2);

        return new MacroInterpretation(
            name,
            score,
            ClassifyReading(score),
            factorNames,
            explanation);
    }

    private static string ClassifyReading(decimal pressureScore) => pressureScore switch
    {
        >= 0.40m => "Pressure rising",
        >= 0.05m => "Mild pressure",
        <= -0.05m => "Relief",
        _ => "Balanced"
    };

    private static PressurePolarity GetPolarity(string? factorName) =>
        factorName is not null && FactorPressurePolarities.TryGetValue(factorName, out var polarity)
            ? polarity
            : PressurePolarity.NegativeScoreIsPressure;

    private static bool IsMarketComplacency(string? factorName) =>
        string.Equals(factorName, MarketComplacency, StringComparison.OrdinalIgnoreCase);
}

public sealed record MacroInterpretation(
    string Name,
    decimal Score,
    string Reading,
    IReadOnlyList<string> ContributingFactors,
    string Explanation);

public enum PressurePolarity
{
    NegativeScoreIsPressure,
    PositiveScoreIsPressure
}
