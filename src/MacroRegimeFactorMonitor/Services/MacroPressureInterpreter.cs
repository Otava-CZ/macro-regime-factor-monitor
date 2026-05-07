using MacroRegimeFactorMonitor.Domain;

namespace MacroRegimeFactorMonitor.Services;

public sealed class MacroPressureInterpreter
{
    private static readonly IReadOnlyDictionary<string, PressurePolarity> PressurePolarityByFactor =
        new Dictionary<string, PressurePolarity>(StringComparer.OrdinalIgnoreCase)
        {
            ["Inflation Pressure"] = PressurePolarity.RiskOffScoreCreatesPressure,
            ["Inflation Breadth"] = PressurePolarity.RiskOffScoreCreatesPressure,
            ["Energy Shock"] = PressurePolarity.RiskOffScoreCreatesPressure,
            ["Growth Stress"] = PressurePolarity.RiskOffScoreCreatesPressure,
            ["Fiscal/Treasury Stress"] = PressurePolarity.RiskOffScoreCreatesPressure,
            ["Market Complacency"] = PressurePolarity.RiskOnScoreCreatesComplacencyPressure
        };

    public MacroPressureInterpretation Interpret(IEnumerable<FactorScore> factorScores)
    {
        var contributions = factorScores
            .Select(TryMapContribution)
            .OfType<MacroPressureContribution>()
            .ToList();

        var interpretationScore = Math.Round(contributions.Sum(contribution => contribution.PressureScore), 2);
        var contributingFactors = contributions
            .Where(contribution => contribution.PressureScore != 0)
            .OrderByDescending(contribution => Math.Abs(contribution.PressureScore))
            .ToList();

        return new MacroPressureInterpretation(
            interpretationScore,
            ClassifyPressure(interpretationScore),
            DescribePressure(interpretationScore),
            contributingFactors);
    }

    private static MacroPressureContribution? TryMapContribution(FactorScore score)
    {
        var factorName = score.MacroFactor?.Name;
        if (string.IsNullOrWhiteSpace(factorName) || !PressurePolarityByFactor.TryGetValue(factorName, out var polarity))
        {
            return null;
        }

        var pressureScore = polarity switch
        {
            PressurePolarity.RiskOffScoreCreatesPressure => -score.WeightedScore,
            PressurePolarity.RiskOnScoreCreatesComplacencyPressure => score.WeightedScore,
            _ => throw new InvalidOperationException($"Unsupported macro pressure polarity '{polarity}'.")
        };

        return new MacroPressureContribution(
            factorName,
            score.MacroFactor?.Category ?? "Uncategorized",
            Math.Round(pressureScore, 2),
            DescribeContribution(factorName, polarity, pressureScore));
    }

    private static string DescribeContribution(string factorName, PressurePolarity polarity, decimal pressureScore) => polarity switch
    {
        PressurePolarity.RiskOffScoreCreatesPressure when pressureScore > 0 => $"{factorName} is adding macro pressure.",
        PressurePolarity.RiskOffScoreCreatesPressure when pressureScore < 0 => $"{factorName} is relieving macro pressure.",
        PressurePolarity.RiskOnScoreCreatesComplacencyPressure when pressureScore > 0 =>
            "Low volatility/complacency is adding market complacency and mispricing pressure.",
        PressurePolarity.RiskOnScoreCreatesComplacencyPressure when pressureScore < 0 =>
            "Higher volatility is reducing market complacency pressure.",
        _ => $"{factorName} is neutral for macro pressure."
    };

    private static string ClassifyPressure(decimal score) => score switch
    {
        >= 0.75m => "Elevated Macro Pressure",
        >= 0.25m => "Building Macro Pressure",
        <= -0.75m => "Macro Pressure Relief",
        <= -0.25m => "Easing Macro Pressure",
        _ => "Balanced Macro Pressure"
    };

    private static string DescribePressure(decimal score) => score switch
    {
        >= 0.75m => "Risk-off factor stress and/or complacent market pricing point to elevated macro pressure.",
        >= 0.25m => "Macro pressure is building beneath the factor score and should be monitored before adding risk.",
        <= -0.75m => "Factor signals point to broad macro pressure relief.",
        <= -0.25m => "Macro pressure is easing, though individual factor risks may remain.",
        _ => "Positive and negative pressure signals are broadly offsetting."
    };

    private enum PressurePolarity
    {
        RiskOffScoreCreatesPressure,
        RiskOnScoreCreatesComplacencyPressure
    }
}

public sealed record MacroPressureInterpretation(
    decimal InterpretationScore,
    string PressureRegime,
    string Summary,
    IReadOnlyList<MacroPressureContribution> ContributingFactors);

public sealed record MacroPressureContribution(
    string FactorName,
    string Category,
    decimal PressureScore,
    string Interpretation);
