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
            .GroupBy(score => MapToMacroInterpretation(score.MacroFactor?.Category, score.MacroFactor?.Name))
            .Select(group => new CategoryScore(group.Key, Math.Round(group.Sum(score => score.WeightedScore), 2)))
            .OrderByDescending(score => Math.Abs(score.Score))
            .ToList();

        return new DashboardSnapshot(
            latestDate.Value,
            compositeScore,
            ClassifyDominantMacroInterpretation(categoryScores),
            scores,
            categoryScores);
    }

    private static string ClassifyDominantMacroInterpretation(IReadOnlyList<CategoryScore> categoryScores)
    {
        if (categoryScores.Count == 0)
        {
            return "No scores yet";
        }

        return categoryScores
            .OrderByDescending(score => Math.Abs(score.Score))
            .First()
            .Category;
    }

    private static string MapToMacroInterpretation(string? category, string? factorName)
    {
        var combined = $"{category} {factorName}";

        if (ContainsAny(combined, "fiscal", "treasury", "rates", "term premium", "yield"))
        {
            return "fiscal/Treasury stress";
        }

        if (ContainsAny(combined, "growth", "labor", "pmi", "hard-landing"))
        {
            return "hard-landing pressure";
        }

        if (ContainsAny(combined, "market", "complacency", "mispricing", "sentiment", "vix"))
        {
            return "market complacency/mispricing";
        }

        if (ContainsAny(combined, "inflation", "stagflation", "commodities", "energy", "cpi"))
        {
            return "inflation/stagflation pressure";
        }

        return "market complacency/mispricing";
    }

    private static bool ContainsAny(string value, params string[] needles) =>
        needles.Any(needle => value.Contains(needle, StringComparison.OrdinalIgnoreCase));
}

public sealed record DashboardSnapshot(
    DateOnly AsOfDate,
    decimal CompositeScore,
    string Regime,
    IReadOnlyList<FactorScore> FactorScores,
    IReadOnlyList<CategoryScore> CategoryScores)
{
    public static DashboardSnapshot Empty { get; } = new(
        DateOnly.MinValue,
        0,
        "No scores yet",
        [],
        []);
}

public sealed record CategoryScore(string Category, decimal Score);
