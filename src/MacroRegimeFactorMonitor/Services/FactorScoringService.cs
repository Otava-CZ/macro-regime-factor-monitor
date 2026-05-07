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
        var interpretations = BuildInterpretations(scores);

        return new DashboardSnapshot(
            latestDate.Value,
            compositeScore,
            FactorScoreCalculator.ClassifyRegime(compositeScore),
            scores,
            categoryScores,
            interpretations);
    }

    private static List<MacroInterpretation> BuildInterpretations(IReadOnlyCollection<FactorScore> scores)
    {
        var scoresByFactorName = scores
            .Where(score => score.MacroFactor is not null)
            .ToDictionary(score => score.MacroFactor!.Name, StringComparer.OrdinalIgnoreCase);

        return
        [
            CreateInterpretation(
                "Inflation/stagflation pressure",
                "Price pressure breadth plus energy shock; growth stress can turn the signal stagflationary.",
                ["Inflation Pressure", "Inflation Breadth", "Energy Shock", "Growth Stress"],
                scoresByFactorName),
            CreateInterpretation(
                "Fiscal/Treasury stress",
                "Treasury-market and fiscal-financing pressure derived from the measurable rates factor.",
                ["Fiscal/Treasury Stress"],
                scoresByFactorName),
            CreateInterpretation(
                "Hard-landing pressure",
                "Downside activity pressure derived from the measurable growth-stress factor.",
                ["Growth Stress"],
                scoresByFactorName),
            CreateInterpretation(
                "Market complacency/mispricing",
                "Risk-asset pricing complacency derived from the measurable market factor.",
                ["Market Complacency"],
                scoresByFactorName)
        ];
    }

    private static MacroInterpretation CreateInterpretation(
        string name,
        string description,
        IReadOnlyList<string> factorNames,
        IReadOnlyDictionary<string, FactorScore> scoresByFactorName)
    {
        var matchedScores = factorNames
            .Where(scoresByFactorName.ContainsKey)
            .Select(factorName => scoresByFactorName[factorName])
            .ToList();
        var pressureScore = Math.Round(-matchedScores.Sum(score => score.WeightedScore), 2);

        return new MacroInterpretation(
            name,
            description,
            pressureScore,
            ClassifyPressure(pressureScore),
            factorNames);
    }

    private static string ClassifyPressure(decimal pressureScore) => pressureScore switch
    {
        >= 0.75m => "Elevated pressure",
        >= 0.25m => "Moderate pressure",
        <= -0.75m => "Strong relief",
        <= -0.25m => "Mild relief",
        _ => "Neutral"
    };
}

public sealed record DashboardSnapshot(
    DateOnly AsOfDate,
    decimal CompositeScore,
    string Regime,
    IReadOnlyList<FactorScore> FactorScores,
    IReadOnlyList<CategoryScore> CategoryScores,
    IReadOnlyList<MacroInterpretation> MacroInterpretations)
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

public sealed record MacroInterpretation(
    string Name,
    string Description,
    decimal PressureScore,
    string Signal,
    IReadOnlyList<string> SourceFactors);
