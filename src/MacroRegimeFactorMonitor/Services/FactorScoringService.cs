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
        var dashboardFactors = MacroInterpretationScoring.BuildDashboardFactors(scores);
        var macroInterpretations = MacroInterpretationScoring.BuildMacroInterpretations(dashboardFactors);

        return new DashboardSnapshot(
            latestDate.Value,
            compositeScore,
            macroInterpretations.FirstOrDefault()?.Reading ?? "No dominant macro interpretation",
            FactorScoreCalculator.ClassifyRegime(compositeScore),
            dashboardFactors,
            categoryScores,
            macroInterpretations);
    }
}

public sealed record DashboardSnapshot(
    DateOnly AsOfDate,
    decimal CompositeScore,
    string PrimaryMacroInterpretation,
    string CompositeRegimeLabel,
    IReadOnlyList<DashboardFactorScore> FactorScores,
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
