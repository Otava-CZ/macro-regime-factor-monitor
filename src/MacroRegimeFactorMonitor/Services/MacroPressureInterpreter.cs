using MacroRegimeFactorMonitor.Domain;

namespace MacroRegimeFactorMonitor.Services;

public sealed class MacroPressureInterpreter
{
    private static readonly IReadOnlyDictionary<string, FactorPressureMapping> FactorMappings =
        new Dictionary<string, FactorPressureMapping>(StringComparer.OrdinalIgnoreCase)
        {
            ["Inflation Pressure"] = new(
                "Inflation/stagflation pressure",
                "Higher trend inflation points to inflation or stagflation pressure.",
                PressureWhenScoreIsNegative: true),
            ["Inflation Breadth"] = new(
                "Inflation/stagflation pressure",
                "Broad price gains reinforce inflation persistence risk.",
                PressureWhenScoreIsNegative: true),
            ["Energy Shock"] = new(
                "Inflation/stagflation pressure",
                "Energy upside shocks can tighten real incomes and inflation expectations.",
                PressureWhenScoreIsNegative: true),
            ["Growth Stress"] = new(
                "Growth/downturn pressure",
                "Weak activity readings point to growth stress rather than risk support.",
                PressureWhenScoreIsNegative: true),
            ["Fiscal/Treasury Stress"] = new(
                "Fiscal/Treasury pressure",
                "Elevated financing or term-premium stress tightens macro conditions.",
                PressureWhenScoreIsNegative: true),
            ["Market Complacency"] = new(
                "Market complacency/mispricing pressure",
                "Low volatility or relaxed risk pricing signals complacency and possible mispricing pressure.",
                PressureWhenScoreIsNegative: false)
        };

    public IReadOnlyList<MacroPressureInterpretation> BuildInterpretations(IEnumerable<FactorScore> scores)
    {
        var includedScores = scores
            .Where(score => score.MacroFactor is not null)
            .Select(score => new ScoredFactor(
                score.MacroFactor!.Name,
                score.RawScore,
                score.WeightedScore,
                ResolveMapping(score.MacroFactor.Name)))
            .Where(score => score.Mapping is not null)
            .ToList();

        return includedScores
            .GroupBy(score => score.Mapping!.PressureName)
            .Select(group => BuildInterpretation(group.Key, group))
            .OrderByDescending(interpretation => Math.Abs(interpretation.PressureScore))
            .ThenBy(interpretation => interpretation.PressureName)
            .ToList();
    }

    public MacroPressureInterpretation BuildInterpretation(string pressureName, IEnumerable<FactorScore> scores)
    {
        var relevantScores = scores
            .Where(score => score.MacroFactor is not null)
            .Select(score => new ScoredFactor(
                score.MacroFactor!.Name,
                score.RawScore,
                score.WeightedScore,
                ResolveMapping(score.MacroFactor.Name)))
            .Where(score => score.Mapping?.PressureName.Equals(pressureName, StringComparison.OrdinalIgnoreCase) == true)
            .ToList();

        return BuildInterpretation(pressureName, relevantScores);
    }

    public FactorPressurePolarity? GetPolarity(string factorName)
    {
        var mapping = ResolveMapping(factorName);
        if (mapping is null)
        {
            return null;
        }

        return new FactorPressurePolarity(
            factorName,
            mapping.PressureName,
            mapping.PressureWhenScoreIsNegative
                ? "Negative factor scores increase macro pressure; positive scores ease pressure."
                : "Positive factor scores increase macro pressure; negative scores ease pressure.",
            mapping.Explanation);
    }

    private static MacroPressureInterpretation BuildInterpretation(string pressureName, IEnumerable<ScoredFactor> scores)
    {
        var factors = scores.ToList();
        var pressureScore = Math.Round(factors.Sum(score => score.PressureContribution), 2);
        var contributingFactors = factors
            .Select(score => score.FactorName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name)
            .ToList();

        var stance = pressureScore switch
        {
            >= 0.75m => "Elevated pressure",
            >= 0.25m => "Building pressure",
            <= -0.75m => "Easing pressure",
            <= -0.25m => "Mild easing",
            _ => "Balanced / mixed"
        };

        return new MacroPressureInterpretation(
            pressureName,
            pressureScore,
            stance,
            BuildSummary(pressureName, stance, pressureScore, contributingFactors),
            contributingFactors);
    }

    private static string BuildSummary(string pressureName, string stance, decimal pressureScore, IReadOnlyList<string> contributingFactors)
    {
        if (contributingFactors.Count == 0)
        {
            return $"No scored factors are available for {pressureName}.";
        }

        var factorList = string.Join(", ", contributingFactors);
        return $"{stance} in {pressureName} (pressure score {pressureScore}) based on {factorList}.";
    }

    private static FactorPressureMapping? ResolveMapping(string factorName) =>
        FactorMappings.GetValueOrDefault(factorName);

    private sealed record ScoredFactor(
        string FactorName,
        decimal RawScore,
        decimal WeightedScore,
        FactorPressureMapping? Mapping)
    {
        public decimal PressureContribution => Mapping is null
            ? 0m
            : Mapping.PressureWhenScoreIsNegative ? -WeightedScore : WeightedScore;
    }

    private sealed record FactorPressureMapping(
        string PressureName,
        string Explanation,
        bool PressureWhenScoreIsNegative);
}

public sealed record MacroPressureInterpretation(
    string PressureName,
    decimal PressureScore,
    string Stance,
    string Summary,
    IReadOnlyList<string> ContributingFactors);

public sealed record FactorPressurePolarity(
    string FactorName,
    string PressureName,
    string Polarity,
    string Explanation);
