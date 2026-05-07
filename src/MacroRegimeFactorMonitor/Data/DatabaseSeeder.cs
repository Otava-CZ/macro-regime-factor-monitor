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
            CreateFactor("Inflation Pressure", "Inflation", "Tracks whether trend inflation is running hotter than neutral.", 0.22m, false, "Core CPI Trend", "BLS/FRED", "% y/y", 2.5m, 0.5m, 3.1m),
            CreateFactor("Inflation Breadth", "Inflation", "Measures how broadly price pressure is distributed across components.", 0.18m, false, "Trimmed Mean CPI", "Cleveland Fed", "% y/y", 2.4m, 0.4m, 2.9m),
            CreateFactor("Energy Shock", "Commodities", "Captures oil and energy pressure that can spill into inflation expectations.", 0.15m, false, "WTI Crude Oil", "EIA", "USD/bbl", 75m, 12m, 88m),
            CreateFactor("Growth Stress", "Growth", "Summarizes downside stress in activity and labor-market momentum.", 0.20m, true, "ISM Manufacturing PMI", "ISM", "Index", 50m, 2m, 48.6m),
            CreateFactor("Fiscal/Treasury Stress", "Rates", "Monitors Treasury-market and fiscal financing stress.", 0.13m, false, "10Y Treasury Term Premium", "NY Fed", "%", 0.25m, 0.35m, 0.72m),
            CreateFactor("Market Complacency", "Markets", "Identifies whether risk pricing appears too relaxed versus macro risk.", 0.12m, false, "VIX Index", "Cboe", "Index", 18m, 5m, 14.2m)
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

            var weightedScore = FactorScoreCalculator.CalculateWeightedScore(rawScore, item.Factor.Weight);
            var impactPreview = new FactorScore
            {
                MacroFactor = item.Factor,
                ScoreDate = SeedDate,
                RawScore = rawScore,
                WeightedScore = weightedScore,
                RegimeImpact = string.Empty
            };

            db.FactorScores.Add(new FactorScore
            {
                MacroFactorId = item.Factor.Id,
                ScoreDate = SeedDate,
                RawScore = rawScore,
                WeightedScore = weightedScore,
                RegimeImpact = MacroPressureInterpreter.ClassifyFactorImpact(impactPreview),
                Notes = "Seeded from the initial macro factor set."
            });
        }

        db.WeeklyReviews.Add(new WeeklyReview
        {
            WeekEnding = SeedDate,
            RegimeAssessment = "Defensive Slowdown",
            KeyDevelopments = "Seed data shows inflation, energy, and rates stress offsetting growth momentum.",
            RisksToWatch = "Watch whether inflation breadth narrows and whether Treasury stress eases."
        });

        db.TradeIdeas.Add(new TradeIdea
        {
            IdeaDate = SeedDate,
            Title = "Watch defensive equity factor exposure",
            Instrument = "Quality / low-volatility basket",
            Thesis = "Use the journal to track discretionary ideas suggested by the macro dashboard. This is not an execution or broker integration.",
            Status = "Watching",
            RiskNotes = "Reassess if growth data improves or inflation pressure cools."
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
