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

        var interpretations = MacroInterpretation.Build(scores);

        return new DashboardSnapshot(
            latestDate.Value,
            compositeScore,
            FactorScoreCalculator.ClassifyRegime(compositeScore),
            scores,
            categoryScores,
            interpretations);
    }
}

public sealed record DashboardSnapshot(
    DateOnly AsOfDate,
    decimal CompositeScore,
    string Regime,
    IReadOnlyList<FactorScore> FactorScores,
    IReadOnlyList<CategoryScore> CategoryScores,
    IReadOnlyList<MacroInterpretation> Interpretations)
{
    public static DashboardSnapshot Empty { get; } = new(
        DateOnly.MinValue,
        0,
        "No scores yet",
        [],
        [],
        []);
}

public sealed record CategoryScore(string Category, decimal Score);

public sealed record MacroInterpretation(string Name, decimal PressureScore, string Assessment)
{
    public static IReadOnlyList<MacroInterpretation> Build(IReadOnlyList<FactorScore> scores)
    {
        var scoresByFactor = scores.ToDictionary(
            score => score.MacroFactor?.Name ?? string.Empty,
            score => score.WeightedScore,
            StringComparer.OrdinalIgnoreCase);

        return
        [
            Pressure(
                "Inflation/stagflation pressure",
                -Sum(scoresByFactor, "Inflation Pressure", "Inflation Breadth", "Energy Shock")),
            Pressure(
                "Fiscal/Treasury stress",
                -Sum(scoresByFactor, "Fiscal/Treasury Stress")),
            Pressure(
                "Hard-landing pressure",
                -Sum(scoresByFactor, "Growth Stress")),
            Pressure(
                "Market complacency/mispricing",
                Sum(scoresByFactor, "Market Complacency"))
        ];
    }

    private static MacroInterpretation Pressure(string name, decimal pressureScore) => new(
        name,
        Math.Round(pressureScore, 2),
        pressureScore switch
        {
            >= 0.5m => "Elevated",
            >= 0.15m => "Building",
            <= -0.5m => "Easing",
            <= -0.15m => "Contained",
            _ => "Neutral"
        });

    private static decimal Sum(Dictionary<string, decimal> scoresByFactor, params string[] factorNames) =>
        factorNames.Sum(factorName => scoresByFactor.GetValueOrDefault(factorName));
}
