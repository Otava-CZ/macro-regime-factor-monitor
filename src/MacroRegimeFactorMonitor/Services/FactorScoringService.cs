using MacroRegimeFactorMonitor.Data;
using MacroRegimeFactorMonitor.Domain;
using Microsoft.EntityFrameworkCore;

namespace MacroRegimeFactorMonitor.Services;

public sealed class FactorScoringService(IDbContextFactory<MacroRegimeDbContext> dbFactory)
{
    public async Task<DashboardSnapshot> GetLatestSnapshotAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var latestImportedManualDate = await db.FactorScores
            .AsNoTracking()
            .Where(score => score.DataMode == ImportedObservationScoringService.ImportedManualDataMode)
            .MaxAsync(score => (DateOnly?)score.ScoreDate);
        var preferredDataMode = latestImportedManualDate.HasValue
            ? ImportedObservationScoringService.ImportedManualDataMode
            : "Sample";
        var selectedDate = latestImportedManualDate
            ?? await db.FactorScores
                .AsNoTracking()
                .Where(score => score.DataMode == preferredDataMode)
                .MaxAsync(score => (DateOnly?)score.ScoreDate);

        if (selectedDate is null)
        {
            return DashboardSnapshot.Empty;
        }

        var selectedDateScores = await db.FactorScores
            .AsNoTracking()
            .Include(score => score.MacroFactor)
            .Where(score => score.ScoreDate == selectedDate && score.DataMode == preferredDataMode)
            .ToListAsync();

        var preferredVersion = preferredDataMode == ImportedObservationScoringService.ImportedManualDataMode
            ? ImportedObservationScoringService.SelectPreferredImportedManualVersion(selectedDateScores
                .Select(score => score.ScoringModelVersion))
            : null;
        var scores = selectedDateScores
            .Where(score => preferredVersion is null || score.ScoringModelVersion == preferredVersion)
            .OrderBy(score => score.MacroFactor!.Name)
            .ToList();
        var dataModes = scores
            .Select(score => score.DataMode)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(mode => mode)
            .ToList();
        var scoringModelVersions = scores
            .Select(score => score.ScoringModelVersion)
            .Where(version => !string.IsNullOrWhiteSpace(version))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(version => version)
            .ToList();

        var compositeScore = Math.Round(scores.Sum(score => score.WeightedScore), 2);
        var categoryScores = scores
            .GroupBy(score => score.MacroFactor?.Category ?? "Uncategorized")
            .Select(group => new CategoryScore(group.Key, Math.Round(group.Sum(score => score.WeightedScore), 2)))
            .OrderByDescending(score => Math.Abs(score.Score))
            .ToList();
        var macroInterpretations = MacroInterpretationScoring.Build(scores);

        return new DashboardSnapshot(
            selectedDate.Value,
            compositeScore,
            macroInterpretations.FirstOrDefault()?.Reading ?? "No dominant macro interpretation",
            FactorScoreCalculator.ClassifyRegime(compositeScore),
            scores,
            categoryScores,
            macroInterpretations,
            string.Join(", ", dataModes),
            string.Join(", ", scoringModelVersions));
    }

}

public static class MacroInterpretationScoring
{
    private static readonly MacroInterpretationDefinition[] InterpretationDefinitions =
    [
        new(
            "inflation/stagflation pressure",
            ["Inflation Pressure", "Inflation Breadth", "Energy Shock"],
            "Inflation, breadth, and energy factors are converted into pressure contributions; positive contributions mean the factors are adding inflation or stagflation pressure."),
        new(
            "fiscal/Treasury stress",
            ["Fiscal/Treasury Stress"],
            "Treasury-market stress is read as pressure when the scored factor moves away from baseline in the stress direction."),
        new(
            "hard-landing pressure",
            ["Growth Stress"],
            "Growth stress is read as pressure when activity momentum weakens versus its baseline."),
        new(
            "market complacency/mispricing",
            ["Market Complacency"],
            "Low-volatility complacency is treated as possible mispricing pressure, rather than a bullish signal.")
    ];

    public static IReadOnlyList<MacroInterpretation> Build(IReadOnlyList<FactorScore> scores)
    {
        var scoreByFactor = scores
            .Where(score => score.MacroFactor is not null)
            .ToDictionary(score => score.MacroFactor!.Name, score => score, StringComparer.OrdinalIgnoreCase);

        return InterpretationDefinitions
            .Select(definition => CreateInterpretation(definition, scoreByFactor))
            .OrderByDescending(interpretation => interpretation.Score)
            .ThenBy(interpretation => interpretation.Name)
            .ToList();
    }

    private static MacroInterpretation CreateInterpretation(
        MacroInterpretationDefinition definition,
        Dictionary<string, FactorScore> scoreByFactor)
    {
        var contributingScores = definition.FactorNames
            .Where(scoreByFactor.ContainsKey)
            .Select(factorName => scoreByFactor[factorName])
            .ToList();
        var score = contributingScores.Sum(FactorScoreCalculator.CalculatePressureContribution);
        var supportingFactors = contributingScores.Count > 0
            ? string.Join(", ", contributingScores.Select(score => score.MacroFactor!.Name))
            : "No scored factors";
        var factorContributions = contributingScores.Count > 0
            ? string.Join("; ", contributingScores.Select(score =>
            {
                var pressureContribution = FactorScoreCalculator.CalculatePressureContribution(score);
                return $"{score.MacroFactor!.Name}: {pressureContribution:+0.00;-0.00;0.00} ({FactorScoreCalculator.ClassifyPressureImpact(pressureContribution, score.MacroFactor.Name)})";
            }))
            : "No scored factor contributions yet.";

        return new(
            definition.Name,
            Math.Round(score, 2),
            ClassifyInterpretation(definition.Name, score),
            supportingFactors,
            factorContributions,
            definition.Explanation);
    }

    private static string ClassifyInterpretation(string name, decimal score) => score switch
    {
        >= 0.75m => $"Elevated {name}",
        >= 0.25m => $"Building {name}",
        <= -0.25m => $"Low {name}",
        _ => $"Neutral {name}"
    };

    private sealed record MacroInterpretationDefinition(
        string Name,
        IReadOnlyList<string> FactorNames,
        string Explanation);

}

public sealed record DashboardSnapshot(
    DateOnly AsOfDate,
    decimal CompositeScore,
    string PrimaryMacroInterpretation,
    string CompositeRegimeLabel,
    IReadOnlyList<FactorScore> FactorScores,
    IReadOnlyList<CategoryScore> CategoryScores,
    IReadOnlyList<MacroInterpretation> MacroInterpretations,
    string DataMode,
    string ScoringModelVersion)
{
    public static DashboardSnapshot Empty { get; } = new(
        DateOnly.MinValue,
        0,
        "No macro interpretation yet",
        "No scores yet",
        [],
        [],
        [],
        string.Empty,
        string.Empty);
}

public sealed record CategoryScore(string Category, decimal Score);

public sealed record MacroInterpretation(
    string Name,
    decimal Score,
    string Reading,
    string SupportingFactors,
    string FactorContributions,
    string Explanation);
