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

        var factorContributions = MacroPressureInterpreter.BuildFactorContributions(scores);
        var interpretations = MacroPressureInterpreter.BuildInterpretations(factorContributions);

        return new DashboardSnapshot(
            latestDate.Value,
            compositeScore,
            FactorScoreCalculator.ClassifyRegime(compositeScore),
            scores,
            categoryScores,
            factorContributions,
            interpretations);
    }
}

public sealed record DashboardSnapshot(
    DateOnly AsOfDate,
    decimal CompositeScore,
    string Regime,
    IReadOnlyList<FactorScore> FactorScores,
    IReadOnlyList<CategoryScore> CategoryScores,
    IReadOnlyList<FactorPressureContribution> FactorContributions,
    IReadOnlyList<MacroInterpretation> Interpretations)
{
    public static DashboardSnapshot Empty { get; } = new(
        DateOnly.MinValue,
        0,
        "No scores yet",
        [],
        [],
        [],
        []);
}

public sealed record CategoryScore(string Category, decimal Score);
