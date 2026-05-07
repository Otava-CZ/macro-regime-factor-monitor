using MacroRegimeFactorMonitor.Domain;

namespace MacroRegimeFactorMonitor.Services;

public static class MacroInterpretationScoring
{
    private static readonly IReadOnlyDictionary<string, FactorPressureMapping> FactorMappings =
        new Dictionary<string, FactorPressureMapping>(StringComparer.OrdinalIgnoreCase)
        {
            ["Inflation Pressure"] = new("Inflation Pressure", FactorPressurePolarity.NegativeWeightedScoreIsPressure),
            ["Inflation Breadth"] = new("Inflation Breadth", FactorPressurePolarity.NegativeWeightedScoreIsPressure),
            ["Energy Shock"] = new("Energy Shock", FactorPressurePolarity.NegativeWeightedScoreIsPressure),
            ["Growth Stress"] = new("Growth Stress", FactorPressurePolarity.NegativeWeightedScoreIsPressure),
            ["Fiscal/Treasury Stress"] = new("Fiscal/Treasury Stress", FactorPressurePolarity.NegativeWeightedScoreIsPressure),
            ["Market Complacency"] = new("Market Complacency", FactorPressurePolarity.PositiveWeightedScoreIsPressure)
        };

    private static readonly IReadOnlyList<InterpretationDefinition> InterpretationDefinitions =
    [
        new(
            "inflation/stagflation pressure",
            ["Inflation Pressure", "Inflation Breadth", "Energy Shock"],
            "Inflation/stagflation pressure rises when inflation, breadth, or energy factors move away from relief and toward macro pressure."),
        new(
            "fiscal/Treasury stress",
            ["Fiscal/Treasury Stress"],
            "Fiscal/Treasury stress rises when the Treasury-market stress factor moves away from relief and toward macro pressure."),
        new(
            "hard-landing pressure",
            ["Growth Stress"],
            "Hard-landing pressure rises when the Growth Stress factor shows weaker activity or labor-market momentum."),
        new(
            "market complacency/mispricing",
            ["Market Complacency"],
            "Market complacency/mispricing is building when the Market Complacency factor indicates volatility is below its baseline.")
    ];

    public static IReadOnlyList<DashboardFactorScore> BuildDashboardFactors(IReadOnlyList<FactorScore> scores) =>
        scores
            .Select(score =>
            {
                var factorName = score.MacroFactor?.Name ?? string.Empty;
                var pressureContribution = CalculatePressureContribution(factorName, score.WeightedScore);

                return new DashboardFactorScore(
                    score,
                    Math.Round(pressureContribution, 2),
                    ClassifyFactorImpact(factorName, pressureContribution));
            })
            .ToList();

    public static IReadOnlyList<MacroInterpretation> BuildMacroInterpretations(IReadOnlyList<DashboardFactorScore> dashboardFactors)
    {
        var pressureByFactor = dashboardFactors
            .Where(score => score.Factor.MacroFactor is not null)
            .ToDictionary(
                score => score.Factor.MacroFactor!.Name,
                score => score.PressureContribution,
                StringComparer.OrdinalIgnoreCase);

        return InterpretationDefinitions
            .Select(definition => CreateInterpretation(definition, pressureByFactor))
            .OrderByDescending(interpretation => interpretation.Score)
            .ThenBy(interpretation => interpretation.Name)
            .ToList();
    }

    public static decimal CalculatePressureContribution(string factorName, decimal weightedScore)
    {
        if (!FactorMappings.TryGetValue(factorName, out var mapping))
        {
            return weightedScore;
        }

        return mapping.Polarity switch
        {
            FactorPressurePolarity.NegativeWeightedScoreIsPressure => -weightedScore,
            FactorPressurePolarity.PositiveWeightedScoreIsPressure => weightedScore,
            _ => weightedScore
        };
    }

    public static string ClassifyFactorImpact(string factorName, decimal pressureContribution)
    {
        if (factorName.Equals("Market Complacency", StringComparison.OrdinalIgnoreCase))
        {
            return pressureContribution switch
            {
                >= 0.05m => "Complacency pressure",
                >= 0.02m => "Mild complacency pressure",
                <= -0.02m => "Volatility relief",
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

    private static MacroInterpretation CreateInterpretation(
        InterpretationDefinition definition,
        IReadOnlyDictionary<string, decimal> pressureByFactor)
    {
        var score = definition.FactorNames.Sum(factorName => pressureByFactor.GetValueOrDefault(factorName));
        return new MacroInterpretation(
            definition.Name,
            Math.Round(score, 2),
            ClassifyInterpretation(definition.Name, score),
            string.Join(", ", definition.FactorNames),
            definition.Explanation);
    }

    private static string ClassifyInterpretation(string name, decimal score) => score switch
    {
        >= 0.15m => $"Elevated {name}",
        >= 0.05m => $"Building {name}",
        <= -0.05m => $"Relief in {name}",
        _ => $"Neutral {name}"
    };

    private sealed record InterpretationDefinition(string Name, IReadOnlyList<string> FactorNames, string Explanation);

    private sealed record FactorPressureMapping(string FactorName, FactorPressurePolarity Polarity);

    private enum FactorPressurePolarity
    {
        NegativeWeightedScoreIsPressure,
        PositiveWeightedScoreIsPressure
    }
}

public sealed record DashboardFactorScore(FactorScore Factor, decimal PressureContribution, string DashboardImpact);

public sealed record MacroInterpretation(
    string Name,
    decimal Score,
    string Reading,
    string SupportingFactors,
    string Explanation);
