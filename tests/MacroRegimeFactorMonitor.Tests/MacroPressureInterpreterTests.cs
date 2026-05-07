using MacroRegimeFactorMonitor.Domain;
using MacroRegimeFactorMonitor.Services;
using Xunit;

namespace MacroRegimeFactorMonitor.Tests;

public sealed class MacroPressureInterpreterTests
{
    private readonly MacroPressureInterpreter interpreter = new();

    [Fact]
    public void BuildInterpretation_OnlyListsFactorsWithScoresIncludedInInterpretation()
    {
        var interpretation = interpreter.BuildInterpretation(
            "Inflation/stagflation pressure",
            [CreateScore("Inflation Pressure", -1.20m, -0.26m)]);

        Assert.Equal("Inflation/stagflation pressure", interpretation.PressureName);
        Assert.Equal(new[] { "Inflation Pressure" }, interpretation.ContributingFactors);
        Assert.DoesNotContain("Inflation Breadth", interpretation.ContributingFactors);
        Assert.DoesNotContain("Energy Shock", interpretation.ContributingFactors);
    }

    [Fact]
    public void BuildInterpretations_AllSixSeededFactorsListsAllRelevantScoredFactors()
    {
        var interpretations = interpreter.BuildInterpretations(
            [
                CreateScore("Inflation Pressure", -1.20m, -0.26m),
                CreateScore("Inflation Breadth", -1.25m, -0.23m),
                CreateScore("Energy Shock", -1.08m, -0.16m),
                CreateScore("Growth Stress", -0.70m, -0.14m),
                CreateScore("Fiscal/Treasury Stress", -1.34m, -0.17m),
                CreateScore("Market Complacency", 0.76m, 0.09m)
            ]);

        var allContributors = interpretations
            .SelectMany(interpretation => interpretation.ContributingFactors)
            .OrderBy(factor => factor)
            .ToList();

        Assert.Equal(
            new[]
            {
                "Energy Shock",
                "Fiscal/Treasury Stress",
                "Growth Stress",
                "Inflation Breadth",
                "Inflation Pressure",
                "Market Complacency"
            },
            allContributors);
    }

    [Fact]
    public void MarketComplacency_PositiveRiskAssetScoreMapsToComplacencyPressure()
    {
        var interpretation = interpreter.BuildInterpretation(
            "Market complacency/mispricing pressure",
            [CreateScore("Market Complacency", 0.76m, 0.09m)]);

        Assert.Equal(0.09m, interpretation.PressureScore);
        Assert.Equal(new[] { "Market Complacency" }, interpretation.ContributingFactors);
    }

    private static FactorScore CreateScore(string factorName, decimal rawScore, decimal weightedScore) => new()
    {
        MacroFactor = new MacroFactor
        {
            Name = factorName,
            Category = "Test",
            Weight = 1m,
            HigherIsRiskOn = true
        },
        RawScore = rawScore,
        WeightedScore = weightedScore,
        RegimeImpact = "Test",
        ScoreDate = new DateOnly(2026, 4, 30)
    };
}
