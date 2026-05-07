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

        var regime = FactorScoreCalculator.ClassifyRegime(compositeScore);

        return new DashboardSnapshot(
            latestDate.Value,
            compositeScore,
            regime,
            scores,
            categoryScores,
            BuildInterpretation(regime, compositeScore, scores));
    }

    private static MacroInterpretation BuildInterpretation(string regime, decimal compositeScore, IReadOnlyList<FactorScore> scores)
    {
        var largestDrag = scores.MinBy(score => score.WeightedScore);
        var largestSupport = scores.MaxBy(score => score.WeightedScore);
        var highStressCount = scores.Count(score => score.RawScore <= -1.0m);
        var supportCount = scores.Count(score => score.RawScore >= 1.0m);

        var summary = compositeScore switch
        {
            >= 1.5m => "Macro conditions are broadly supportive of risk-taking, with positive factor breadth.",
            >= 0.4m => "Macro conditions lean constructive, but require confirmation from factor breadth.",
            <= -1.5m => "Macro conditions point to contraction or risk-off pressure, so capital preservation should dominate.",
            <= -0.4m => "Macro conditions lean defensive, with enough pressure to warrant tighter risk review.",
            _ => "Macro conditions are mixed and transitional, making confirmation more important than prediction."
        };

        var riskPosture = compositeScore switch
        {
            >= 1.5m => "Risk-on bias",
            >= 0.4m => "Selective risk-on",
            <= -1.5m => "Risk-off / defensive",
            <= -0.4m => "Defensive slowdown",
            _ => "Neutral / wait for confirmation"
        };

        var focus = highStressCount > supportCount
            ? "Prioritize the weakest factors and journal any trade ideas with explicit invalidation levels."
            : "Watch whether supportive factors broaden enough to outweigh the main macro drag.";

        return new MacroInterpretation(
            summary,
            riskPosture,
            largestDrag?.MacroFactor?.Name ?? "No drag identified",
            largestSupport?.MacroFactor?.Name ?? "No support identified",
            focus,
            regime);
    }
}

public sealed record DashboardSnapshot(
    DateOnly AsOfDate,
    decimal CompositeScore,
    string Regime,
    IReadOnlyList<FactorScore> FactorScores,
    IReadOnlyList<CategoryScore> CategoryScores,
    MacroInterpretation Interpretation)
{
    public static DashboardSnapshot Empty { get; } = new(
        DateOnly.MinValue,
        0,
        "No scores yet",
        [],
        [],
        new MacroInterpretation(
            "No persisted factor scores are available yet.",
            "No posture",
            "No drag identified",
            "No support identified",
            "Seed or import observations to generate an interpretation.",
            "No scores yet"));
}

public sealed record CategoryScore(string Category, decimal Score);

public sealed record MacroInterpretation(
    string Summary,
    string RiskPosture,
    string PrimaryDrag,
    string PrimarySupport,
    string ReviewFocus,
    string RegimeLabel);
