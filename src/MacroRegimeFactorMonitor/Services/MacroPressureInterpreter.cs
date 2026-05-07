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

    private static readonly InterpretationDefinition[] Definitions =
    [
        new(
            "Inflation/stagflation pressure",
            [InflationPressure, InflationBreadth, EnergyShock],
            "Inflation/stagflation pressure reflects whether measured inflation, price breadth, and energy shocks are adding macro pressure."),
        new(
            "Fiscal/Treasury stress",
            [FiscalTreasuryStress],
            "Fiscal/Treasury stress reflects whether Treasury-market and fiscal financing conditions are adding macro pressure."),
        new(
            "Hard-landing pressure",
            [GrowthStress],
            "Hard-landing pressure reflects whether growth momentum is deteriorating enough to add downside macro pressure."),
        new(
            "Market complacency/mispricing",
            [MarketComplacency],
            "Market complacency/mispricing pressure reflects whether volatility pricing looks too relaxed versus macro risk.")
    ];

    public static IReadOnlyList<FactorPressureContribution> BuildFactorContributions(IEnumerable<FactorScore> scores) =>
        scores
            .Select(score =>
            {
                var factorName = score.MacroFactor?.Name ?? string.Empty;
                var pressureScore = ConvertToPressureContribution(factorName, score.WeightedScore);

                return new FactorPressureContribution(
                    score,
                    pressureScore,
                    ClassifyFactorImpact(factorName, pressureScore),
                    ExplainFactorContribution(factorName, pressureScore));
            })
            .ToList();

    public static IReadOnlyList<MacroInterpretation> BuildInterpretations(IReadOnlyList<FactorPressureContribution> contributions)
    {
        var contributionsByName = contributions
            .Where(contribution => contribution.FactorScore.MacroFactor is not null)
            .ToDictionary(
                contribution => contribution.FactorScore.MacroFactor!.Name,
                contribution => contribution,
                StringComparer.OrdinalIgnoreCase);

        return Definitions
            .Select(definition =>
            {
                var interpretationContributors = definition.FactorNames
                    .Where(contributionsByName.ContainsKey)
                    .Select(factorName => contributionsByName[factorName])
                    .ToList();
                var score = Math.Round(interpretationContributors.Sum(contribution => contribution.PressureScore), 2);

                return new MacroInterpretation(
                    definition.Name,
                    score,
                    ClassifyPressure(score),
                    interpretationContributors,
                    ExplainInterpretation(definition, score, interpretationContributors));
            })
            .ToList();
    }

    public static decimal ConvertToPressureContribution(string factorName, decimal weightedScore)
    {
        var direction = factorName switch
        {
            InflationPressure or InflationBreadth or EnergyShock or GrowthStress or FiscalTreasuryStress => -1,
            MarketComplacency => 1,
            _ => 0
        };

        return Math.Round(weightedScore * direction, 2);
    }

    public static string ClassifyFactorImpact(string factorName, decimal pressureScore)
    {
        if (string.Equals(factorName, MarketComplacency, StringComparison.OrdinalIgnoreCase))
        {
            return pressureScore switch
            {
                >= 0.05m => "Complacency pressure",
                <= -0.05m => "Volatility relief",
                _ => "Balanced"
            };
        }

        return ClassifyPressure(pressureScore);
    }

    public static string ClassifyPressure(decimal pressureScore) => pressureScore switch
    {
        >= 0.20m => "Pressure rising",
        >= 0.05m => "Mild pressure",
        <= -0.20m => "Relief",
        <= -0.05m => "Mild relief",
        _ => "Balanced"
    };

    private static string ExplainFactorContribution(string factorName, decimal pressureScore)
    {
        if (string.Equals(factorName, MarketComplacency, StringComparison.OrdinalIgnoreCase))
        {
            return pressureScore > 0
                ? "The Market Complacency factor indicates volatility is below its baseline, so low volatility is treated as complacency/mispricing pressure."
                : "The Market Complacency factor indicates volatility is not unusually suppressed versus its baseline.";
        }

        return pressureScore switch
        {
            > 0 => $"The {factorName} factor is adding macro pressure under its interpretation polarity.",
            < 0 => $"The {factorName} factor is providing relief under its interpretation polarity.",
            _ => $"The {factorName} factor is balanced under its interpretation polarity."
        };
    }

    private static string ExplainInterpretation(
        InterpretationDefinition definition,
        decimal score,
        IReadOnlyList<FactorPressureContribution> contributors)
    {
        if (contributors.Count == 0)
        {
            return $"{definition.Name} cannot be read yet because no contributing measurable factors are available.";
        }

        var leadingContributor = contributors
            .OrderByDescending(contribution => Math.Abs(contribution.PressureScore))
            .First();
        var factorName = leadingContributor.FactorScore.MacroFactor?.Name ?? "the leading factor";

        if (string.Equals(definition.Name, "Market complacency/mispricing", StringComparison.OrdinalIgnoreCase) && score > 0)
        {
            return "Market complacency/mispricing is building because the Market Complacency factor indicates volatility is below its baseline.";
        }

        var direction = score switch
        {
            > 0 => "is building",
            < 0 => "is easing",
            _ => "is balanced"
        };

        return $"{definition.Name} {direction} because {factorName} is the largest current contributor.";
    }

    private sealed record InterpretationDefinition(
        string Name,
        IReadOnlyList<string> FactorNames,
        string Description);
}

public sealed record FactorPressureContribution(
    FactorScore FactorScore,
    decimal PressureScore,
    string Reading,
    string Explanation);

public sealed record MacroInterpretation(
    string Name,
    decimal Score,
    string Reading,
    IReadOnlyList<FactorPressureContribution> Contributors,
    string Explanation);
