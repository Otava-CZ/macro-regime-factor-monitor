using MacroRegimeFactorMonitor.Domain;
using MacroRegimeFactorMonitor.Services;
using Microsoft.EntityFrameworkCore;

namespace MacroRegimeFactorMonitor.Data;

public static class DatabaseSeeder
{
    private static readonly DateOnly SeedDate = new(2026, 4, 30);

    public static async Task SeedAsync(MacroRegimeDbContext db)
    {
        if (await db.MacroFactors.AnyAsync())
        {
            return;
        }

        var factors = new[]
        {
            CreateFactor("Inflation/stagflation pressure", "Inflation/stagflation pressure", "Tracks whether sticky inflation and commodity pressure are tightening the policy trade-off.", 0.30m, false, "Core CPI Trend", "BLS/FRED", "% y/y", 2.5m, 0.5m, 3.1m),
            CreateFactor("Fiscal/Treasury stress", "Fiscal/Treasury stress", "Monitors Treasury-market and fiscal financing stress through term premium pressure.", 0.25m, false, "10Y Treasury Term Premium", "NY Fed", "%", 0.25m, 0.35m, 0.72m),
            CreateFactor("Hard-landing pressure", "Hard-landing pressure", "Summarizes downside stress in activity and labor-market momentum.", 0.25m, true, "ISM Manufacturing PMI", "ISM", "Index", 50m, 2m, 48.6m),
            CreateFactor("Market complacency/mispricing", "Market complacency/mispricing", "Identifies whether risk pricing appears too relaxed versus macro risks.", 0.20m, true, "VIX Index", "Cboe", "Index", 18m, 5m, 14.2m)
        };

        db.MacroFactors.AddRange(factors.Select(item => item.Factor));
        await db.SaveChangesAsync();

        foreach (var item in factors)
        {
            item.Indicator.MacroFactorId = item.Factor.Id;
            db.Indicators.Add(item.Indicator);
        }

        await db.SaveChangesAsync();

        foreach (var item in factors)
        {
            db.IndicatorObservations.Add(new IndicatorObservation
            {
                IndicatorId = item.Indicator.Id,
                ObservationDate = SeedDate,
                Value = item.ObservationValue,
                Notes = "Initial seed observation."
            });

            var rawScore = FactorScoreCalculator.CalculateRawScore(
                item.ObservationValue,
                item.Indicator.Baseline,
                item.Indicator.Volatility,
                item.Factor.HigherIsRiskOn);

            db.FactorScores.Add(new FactorScore
            {
                MacroFactorId = item.Factor.Id,
                ScoreDate = SeedDate,
                RawScore = rawScore,
                WeightedScore = FactorScoreCalculator.CalculateWeightedScore(rawScore, item.Factor.Weight),
                RegimeImpact = FactorScoreCalculator.ClassifyImpact(rawScore),
                Notes = "Seeded from the initial macro factor set."
            });
        }

        db.WeeklyReviews.Add(new WeeklyReview
        {
            WeekEnding = SeedDate,
            RegimeAssessment = "Inflation/stagflation pressure",
            KeyDevelopments = "Seed data shows inflation/stagflation pressure, Treasury stress, hard-landing pressure, and possible market complacency/mispricing.",
            RisksToWatch = "Watch whether inflation cools, Treasury stress eases, activity stabilizes, and volatility reprices macro risk."
        });

        db.TradeIdeas.Add(new TradeIdea
        {
            IdeaDate = SeedDate,
            Title = "Watch defensive equity factor exposure",
            Instrument = "Quality / low-volatility basket",
            Thesis = "Use the journal to track discretionary ideas suggested by the macro dashboard. This is not an execution or broker integration.",
            Status = "Watching",
            RiskNotes = "Reassess if growth data improves or inflation pressure cools.",
            EntryTrigger = "Only consider after the dashboard still shows macro pressure and price action confirms defensive leadership.",
            Invalidation = "Invalidate if inflation pressure cools, activity data rebounds, or defensive leadership fails.",
            Catalyst = "Weekly review identifies persistent inflation/stagflation pressure or hard-landing pressure.",
            MaxLoss = "Pre-defined discretionary risk budget; no automatic orders are placed.",
            TimeHorizon = "Two to six weeks, reviewed weekly.",
            PostMortem = "Complete after the idea is closed to compare thesis, trigger, catalyst, and outcome."
        });

        await db.SaveChangesAsync();
    }

    private static (MacroFactor Factor, Indicator Indicator, decimal ObservationValue) CreateFactor(
        string name,
        string category,
        string description,
        decimal weight,
        bool higherIsRiskOn,
        string indicatorName,
        string source,
        string unit,
        decimal baseline,
        decimal volatility,
        decimal observationValue) =>
        (new MacroFactor
        {
            Name = name,
            Category = category,
            Description = description,
            Weight = weight,
            HigherIsRiskOn = higherIsRiskOn
        },
        new Indicator
        {
            Name = indicatorName,
            Source = source,
            Unit = unit,
            Baseline = baseline,
            Volatility = volatility
        },
        observationValue);
}
