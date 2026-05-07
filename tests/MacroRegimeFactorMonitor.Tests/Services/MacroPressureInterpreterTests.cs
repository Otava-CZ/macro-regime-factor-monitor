using MacroRegimeFactorMonitor.Domain;
using MacroRegimeFactorMonitor.Services;

namespace MacroRegimeFactorMonitor.Tests.Services;

public sealed class MacroPressureInterpreterTests
{
    [Fact]
    public void Interpret_MapsRiskOffFactorStressToPositiveMacroPressure()
    {
        var interpreter = new MacroPressureInterpreter();
        var scores = new[]
        {
            CreateScore("Inflation Pressure", "Inflation", -0.26m)
        };

        var interpretation = interpreter.Interpret(scores);

        Assert.Equal(0.26m, interpretation.InterpretationScore);
        Assert.Equal("Building Macro Pressure", interpretation.PressureRegime);
        var contribution = Assert.Single(interpretation.ContributingFactors);
        Assert.Equal("Inflation Pressure", contribution.FactorName);
        Assert.Equal(0.26m, contribution.PressureScore);
    }

    [Fact]
    public void Interpret_MapsLowVolatilityMarketComplacencyToPositivePressure()
    {
        var interpreter = new MacroPressureInterpreter();
        var scores = new[]
        {
            CreateScore("Market Complacency", "Markets", 0.09m)
        };

        var interpretation = interpreter.Interpret(scores);

        Assert.Equal(0.09m, interpretation.InterpretationScore);
        var contribution = Assert.Single(interpretation.ContributingFactors);
        Assert.Equal("Market Complacency", contribution.FactorName);
        Assert.Equal(0.09m, contribution.PressureScore);
        Assert.Contains("complacency", contribution.Interpretation, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("mispricing", contribution.Interpretation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Interpret_OnlyListsFactorsIncludedInInterpretationScoreAsContributors()
    {
        var interpreter = new MacroPressureInterpreter();
        var scores = new[]
        {
            CreateScore("Inflation Pressure", "Inflation", -0.26m),
            CreateScore("Experimental v0.5 Factor", "Research", -1.00m)
        };

        var interpretation = interpreter.Interpret(scores);

        Assert.Equal(0.26m, interpretation.InterpretationScore);
        var contribution = Assert.Single(interpretation.ContributingFactors);
        Assert.Equal("Inflation Pressure", contribution.FactorName);
    }

    private static FactorScore CreateScore(string factorName, string category, decimal weightedScore) => new()
    {
        MacroFactor = new MacroFactor
        {
            Name = factorName,
            Category = category,
            Weight = 1m,
            HigherIsRiskOn = true
        },
        ScoreDate = new DateOnly(2026, 4, 30),
        RawScore = weightedScore,
        WeightedScore = weightedScore,
        RegimeImpact = "Test impact"
    };
}
