using MacroRegimeFactorMonitor.Data;
using MacroRegimeFactorMonitor.Domain;
using Microsoft.EntityFrameworkCore;

namespace MacroRegimeFactorMonitor.Services;

public sealed class FactorScoringService(IDbContextFactory<MacroRegimeDbContext> dbFactory)
{
    public async Task<DashboardSnapshot> GetLatestSnapshotAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var latestDate = await db.FactorScores.MaxAsync(score => (DateOnly?)score.ScoreDate);
        if (latestDate is null)
        {
            return DashboardSnapshot.Empty;
        }

        var scores = await db.FactorScores
            .AsNoTracking()
            .Include(score => score.MacroFactor)
            .Where(score => score.ScoreDate == latestDate)
            .OrderBy(score => score.MacroFactor!.Name)
            .ToListAsync();

        var compositeScore = Math.Round(scores.Sum(score => score.WeightedScore), 2);
        var categoryScores = scores
            .GroupBy(score => score.MacroFactor?.Category ?? "Uncategorized")
            .Select(group => new CategoryScore(group.Key, Math.Round(group.Sum(score => score.WeightedScore), 2)))
            .OrderByDescending(score => Math.Abs(score.Score))
            .ToList();
        var macroInterpretations = BuildMacroInterpretations(scores);

        return new DashboardSnapshot(
            latestDate.Value,
            compositeScore,
            macroInterpretations.FirstOrDefault()?.Reading ?? "No dominant macro interpretation",
            FactorScoreCalculator.ClassifyRegime(compositeScore),
            scores,
            categoryScores,
            macroInterpretations);
    }

    private static IReadOnlyList<MacroInterpretation> BuildMacroInterpretations(IReadOnlyList<FactorScore> scores)
    {
        var scoreByFactor = scores
            .Where(score => score.MacroFactor is not null)
            .ToDictionary(score => score.MacroFactor!.Name, score => score.WeightedScore, StringComparer.OrdinalIgnoreCase);

        var interpretations = new[]
        {
            CreateInterpretation(
                "inflation/stagflation pressure",
                Pressure(scoreByFactor, "Inflation Pressure") + Pressure(scoreByFactor, "Inflation Breadth") + Pressure(scoreByFactor, "Energy Shock"),
                "Inflation Pressure, Inflation Breadth, Energy Shock"),
            CreateInterpretation(
                "fiscal/Treasury stress",
                Pressure(scoreByFactor, "Fiscal/Treasury Stress"),
                "Fiscal/Treasury Stress"),
            CreateInterpretation(
                "hard-landing pressure",
                Pressure(scoreByFactor, "Growth Stress"),
                "Growth Stress"),
            CreateInterpretation(
                "market complacency/mispricing",
                Support(scoreByFactor, "Market Complacency"),
                "Market Complacency")
        };

        return interpretations
            .OrderByDescending(interpretation => interpretation.Score)
            .ThenBy(interpretation => interpretation.Name)
            .ToList();
    }

    private static MacroInterpretation CreateInterpretation(string name, decimal score, string supportingFactors) =>
        new(name, Math.Round(score, 2), ClassifyInterpretation(name, score), supportingFactors);

    private static string ClassifyInterpretation(string name, decimal score) => score switch
    {
        >= 0.75m => $"Elevated {name}",
        >= 0.25m => $"Building {name}",
        <= -0.25m => $"Low {name}",
        _ => $"Neutral {name}"
    };

    private static decimal Pressure(Dictionary<string, decimal> scores, string factorName) =>
        scores.TryGetValue(factorName, out var score) ? -score : 0;

    private static decimal Support(Dictionary<string, decimal> scores, string factorName) =>
        scores.TryGetValue(factorName, out var score) ? score : 0;
}

public sealed record DashboardSnapshot(
    DateOnly AsOfDate,
    decimal CompositeScore,
    string PrimaryMacroInterpretation,
    string CompositeRegimeLabel,
    IReadOnlyList<FactorScore> FactorScores,
    IReadOnlyList<CategoryScore> CategoryScores,
    IReadOnlyList<MacroInterpretation> MacroInterpretations)
{
    public static DashboardSnapshot Empty { get; } = new(
        DateOnly.MinValue,
        0,
        "No macro interpretation yet",
        "No scores yet",
        [],
        [],
        []);
}

public sealed record CategoryScore(string Category, decimal Score);

public sealed record MacroInterpretation(string Name, decimal Score, string Reading, string SupportingFactors);
