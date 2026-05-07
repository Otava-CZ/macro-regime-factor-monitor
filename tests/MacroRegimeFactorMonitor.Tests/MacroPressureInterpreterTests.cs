using MacroRegimeFactorMonitor.Domain;
using MacroRegimeFactorMonitor.Services;
using Xunit;

namespace MacroRegimeFactorMonitor.Tests;

public sealed class MacroPressureInterpreterTests
{
    [Fact]
    public void MarketComplacencyLowVolatilityScoreBuildsMispricingPressure()
    {
        var score = CreateScore("Market Complacency", rawScore: 0.76m, weightedScore: 0.09m);

        var pressureContribution = MacroPressureInterpreter.CalculatePressureContribution(score);
        var factorImpact = MacroPressureInterpreter.ClassifyFactorImpact(score);
        var interpretation = MacroPressureInterpreter.BuildInterpretations([score])
            .Single(item => item.Name == "market complacency/mispricing");

        Assert.Equal(0.09m, pressureContribution);
        Assert.Equal("Complacency pressure", factorImpact);
        Assert.Equal(0.09m, interpretation.Score);
        Assert.Equal("Mild pressure", interpretation.Reading);
    }

    [Fact]
    public void InflationNegativeWeightedScoreBuildsInflationPressure()
    {
        var score = CreateScore("Inflation Pressure", rawScore: -1.20m, weightedScore: -0.26m);

        var pressureContribution = MacroPressureInterpreter.CalculatePressureContribution(score);
        var factorImpact = MacroPressureInterpreter.ClassifyFactorImpact(score);
        var interpretation = MacroPressureInterpreter.BuildInterpretations([score])
            .Single(item => item.Name == "inflation/stagflation pressure");

        Assert.Equal(0.26m, pressureContribution);
        Assert.Equal("Pressure rising", factorImpact);
        Assert.Equal(0.26m, interpretation.Score);
        Assert.Equal("Mild pressure", interpretation.Reading);
    }

    private static FactorScore CreateScore(string factorName, decimal rawScore, decimal weightedScore) => new()
    {
        MacroFactor = new MacroFactor
        {
            Name = factorName,
            Category = "Test",
            Weight = 1m,
            HigherIsRiskOn = false
        },
        ScoreDate = new DateOnly(2026, 4, 30),
        RawScore = rawScore,
        WeightedScore = weightedScore,
        RegimeImpact = string.Empty
    };
}
