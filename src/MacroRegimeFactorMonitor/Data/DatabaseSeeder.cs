using MacroRegimeFactorMonitor.Domain;
using MacroRegimeFactorMonitor.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MacroRegimeFactorMonitor.Data;

public static class DatabaseSeeder
{
    private static readonly DateOnly SeedDate = new(2026, 4, 30);

    public static async Task<DatabaseSeedResult> SeedAsync(
        MacroRegimeDbContext db,
        ILogger? logger = null)
    {
        logger?.LogInformation("Seeding source registry with additive-only synchronization.");

        var result = new DatabaseSeedResult
        {
            SeededDataSources = await SeedDataSourcesAsync(db)
        };

        if (await HasExistingSampleDataAsync(db))
        {
            result.SeededExternalSeries = await SeedFredExternalSeriesAsync(db, logger);
            result.Message = "Source registry synchronized; initial sample macro data skipped because MacroFactors already exist.";
            logger?.LogInformation("Initial sample data skipped because MacroFactors already exist.");
            return result;
        }

        await SeedInitialSampleDataAsync(db, result);
        result.SeededExternalSeries = await SeedFredExternalSeriesAsync(db, logger);
        result.Message = "Source registry synchronized; initial sample macro data inserted into an empty database.";
        return result;
    }

    private static async Task<bool> HasExistingSampleDataAsync(MacroRegimeDbContext db) =>
        await db.MacroFactors.AnyAsync();

    private static async Task SeedInitialSampleDataAsync(
        MacroRegimeDbContext db,
        DatabaseSeedResult result)
    {
        var factors = CreateSeedFactors();

        await SeedMacroFactorsAsync(db, factors, result);
        await SeedIndicatorsAsync(db, factors, result);
        SeedObservationsAndScores(db, factors, result);
        SeedWeeklyReview(db, result);
        SeedTradeIdea(db, result);

        await db.SaveChangesAsync();
    }

    private static async Task SeedMacroFactorsAsync(
        MacroRegimeDbContext db,
        IReadOnlyCollection<(MacroFactor Factor, Indicator Indicator, decimal ObservationValue)> factors,
        DatabaseSeedResult result)
    {
        db.MacroFactors.AddRange(factors.Select(item => item.Factor));
        await db.SaveChangesAsync();
        result.SeededMacroFactors = factors.Count;
    }

    private static async Task SeedIndicatorsAsync(
        MacroRegimeDbContext db,
        IReadOnlyCollection<(MacroFactor Factor, Indicator Indicator, decimal ObservationValue)> factors,
        DatabaseSeedResult result)
    {
        foreach (var item in factors)
        {
            item.Indicator.MacroFactorId = item.Factor.Id;
            db.Indicators.Add(item.Indicator);
        }

        await db.SaveChangesAsync();
        result.SeededIndicators = factors.Count;
    }

    private static void SeedObservationsAndScores(
        MacroRegimeDbContext db,
        IReadOnlyCollection<(MacroFactor Factor, Indicator Indicator, decimal ObservationValue)> factors,
        DatabaseSeedResult result)
    {
        foreach (var item in factors)
        {
            db.IndicatorObservations.Add(CreateObservation(item.Indicator.Id, item.ObservationValue));
            db.FactorScores.Add(CreateFactorScore(item));
        }

        result.SeededObservations = factors.Count;
        result.SeededFactorScores = factors.Count;
    }

    private static IndicatorObservation CreateObservation(int indicatorId, decimal observationValue) =>
        new()
        {
            IndicatorId = indicatorId,
            ObservationDate = SeedDate,
            Value = observationValue,
            Notes = "Initial seed observation."
        };

    private static FactorScore CreateFactorScore(
        (MacroFactor Factor, Indicator Indicator, decimal ObservationValue) item)
    {
        var rawScore = FactorScoreCalculator.CalculateRawScore(
            item.ObservationValue,
            item.Indicator.Baseline,
            item.Indicator.Volatility,
            item.Factor.HigherIsRiskOn);
        var weightedScore = FactorScoreCalculator.CalculateWeightedScore(rawScore, item.Factor.Weight);
        var pressureContribution = FactorScoreCalculator.CalculatePressureContribution(weightedScore, item.Factor.Name);

        return new FactorScore
        {
            MacroFactorId = item.Factor.Id,
            ScoreDate = SeedDate,
            RawScore = rawScore,
            WeightedScore = weightedScore,
            RegimeImpact = FactorScoreCalculator.ClassifyImpact(pressureContribution, item.Factor.Name),
            Notes = "Seeded from the initial macro factor set.",
            DataMode = "Sample",
            ScoringModelVersion = "sample-v0",
            SourceObservationCount = 0
        };
    }

    private static void SeedWeeklyReview(MacroRegimeDbContext db, DatabaseSeedResult result)
    {
        db.WeeklyReviews.Add(new WeeklyReview
        {
            WeekEnding = SeedDate,
            RegimeAssessment = "Inflation and Treasury stress with hard-landing watch",
            KeyDevelopments = "Seed data shows inflation, energy, and rates stress offsetting growth momentum.",
            RisksToWatch = "Watch whether inflation breadth narrows and whether Treasury stress eases."
        });
        result.SeededWeeklyReviews = 1;
    }

    private static void SeedTradeIdea(MacroRegimeDbContext db, DatabaseSeedResult result)
    {
        db.TradeIdeas.Add(new TradeIdea
        {
            IdeaDate = SeedDate,
            Title = "Watch defensive equity factor exposure",
            Instrument = "Quality / low-volatility basket",
            Thesis = "Use the journal to track manual trade candidates suggested by factor-derived macro interpretations. This is not an execution or broker integration.",
            Status = "Watching",
            EntryTrigger = "Consider only after confirming the derived inflation/stagflation and hard-landing readings persist in the factor scores.",
            Invalidation = "Invalidate if growth stress fades and inflation breadth moves back toward neutral.",
            Catalyst = "Weekly data update confirms persistent inflation breadth, energy pressure, or Treasury stress.",
            MaxLoss = "Define before any manual action; no automatic order management is provided.",
            TimeHorizon = "One to eight weeks, reviewed each weekly journal cycle.",
            PostMortem = "Document whether the factor-derived interpretation, entry trigger, and invalidation worked as expected after closing.",
            RiskNotes = "Reassess if growth data improves or inflation pressure cools."
        });
        result.SeededTradeIdeas = 1;
    }

    private static async Task<int> SeedFredExternalSeriesAsync(
        MacroRegimeDbContext db,
        ILogger? logger)
    {
        var fred = await db.DataSources
            .Where(source => source.Name == "FRED")
            .OrderBy(source => source.Id)
            .FirstOrDefaultAsync();

        if (fred is null)
        {
            logger?.LogWarning("Skipping FRED external series seed because the FRED data source is missing.");
            return 0;
        }

        var approvedMappings = CreateApprovedFredMappings();
        var indicatorNames = approvedMappings
            .Select(mapping => mapping.IndicatorName)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var indicatorRows = await db.Indicators
            .Where(indicator => indicatorNames.Contains(indicator.Name))
            .OrderBy(indicator => indicator.Id)
            .ToListAsync();
        var indicators = indicatorRows
            .GroupBy(indicator => indicator.Name, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        var seededCount = 0;
        foreach (var mapping in approvedMappings)
        {
            if (!indicators.TryGetValue(mapping.IndicatorName, out var indicator))
            {
                logger?.LogWarning(
                    "Skipping FRED external series {ExternalSeriesId} because indicator {IndicatorName} is missing.",
                    mapping.ExternalSeriesId,
                    mapping.IndicatorName);
                continue;
            }

            var mappingExists = await db.ExternalSeries.AnyAsync(series =>
                series.DataSourceId == fred.Id &&
                series.ExternalSeriesId == mapping.ExternalSeriesId &&
                series.IndicatorId == indicator.Id);

            if (mappingExists)
            {
                continue;
            }

            db.ExternalSeries.Add(new ExternalSeries
            {
                DataSourceId = fred.Id,
                IndicatorId = indicator.Id,
                ExternalSeriesId = mapping.ExternalSeriesId,
                Endpoint = mapping.Endpoint,
                Frequency = mapping.Frequency,
                Units = mapping.Units,
                Transform = mapping.Transform,
                ObservationDateField = mapping.ObservationDateField,
                ValueField = mapping.ValueField,
                IsActive = true,
                Notes = mapping.Notes
            });
            seededCount++;
        }

        if (seededCount > 0)
        {
            await db.SaveChangesAsync();
        }

        return seededCount;
    }

    private static FredExternalSeriesMapping[] CreateApprovedFredMappings() =>
    [
        new(
            IndicatorName: "Core CPI Trend",
            ExternalSeriesId: "CPILFESL",
            Endpoint: "/series/observations",
            Frequency: "Monthly",
            Units: "PercentChangeFromYearAgo",
            Transform: "pc1",
            ObservationDateField: "date",
            ValueField: "value",
            Notes: "Core CPI less food and energy; FRED units transform pc1 gives percent change from year ago."),
        new(
            IndicatorName: "VIX Index",
            ExternalSeriesId: "VIXCLS",
            Endpoint: "/series/observations",
            Frequency: "Daily",
            Units: "Level",
            Transform: "lin",
            ObservationDateField: "date",
            ValueField: "value",
            Notes: "CBOE volatility index close via FRED; used for market complacency/mispricing factor."),
        new(
            IndicatorName: "10Y Treasury Term Premium",
            ExternalSeriesId: "DGS10",
            Endpoint: "/series/observations",
            Frequency: "Daily",
            Units: "Level",
            Transform: "lin",
            ObservationDateField: "date",
            ValueField: "value",
            Notes: "10-year Treasury constant maturity rate; temporary proxy until a better Treasury stress/term-premium series is selected.")
    ];

    private sealed record FredExternalSeriesMapping(
        string IndicatorName,
        string ExternalSeriesId,
        string Endpoint,
        string Frequency,
        string Units,
        string Transform,
        string ObservationDateField,
        string ValueField,
        string Notes);

    private static async Task<int> SeedDataSourcesAsync(MacroRegimeDbContext db)
    {
        var sourceDefinitions = CreateSourceDefinitions();
        var existingNames = await db.DataSources
            .Select(source => source.Name)
            .ToListAsync();
        var existingNameSet = existingNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missingSources = sourceDefinitions
            .Where(source => !existingNameSet.Contains(source.Name))
            .ToList();

        if (missingSources.Count == 0)
        {
            return 0;
        }

        db.DataSources.AddRange(missingSources);
        await db.SaveChangesAsync();
        return missingSources.Count;
    }

    private static DataSource[] CreateSourceDefinitions() =>
    [
        new()
        {
            Name = "FRED",
            SourceType = "MacroApi",
            BaseUrl = "https://api.stlouisfed.org/fred",
            RequiresApiKey = true
        },
        new()
        {
            Name = "BLS",
            SourceType = "MacroApi",
            BaseUrl = "https://api.bls.gov/publicAPI/v2",
            RequiresApiKey = false
        },
        new()
        {
            Name = "EIA",
            SourceType = "EnergyApi",
            BaseUrl = "https://api.eia.gov/v2",
            RequiresApiKey = true
        },
        new()
        {
            Name = "Treasury Fiscal Data",
            SourceType = "FiscalApi",
            BaseUrl = "https://api.fiscaldata.treasury.gov/services/api/fiscal_service",
            RequiresApiKey = false
        }
    ];

    private static (MacroFactor Factor, Indicator Indicator, decimal ObservationValue)[] CreateSeedFactors() =>
    [
        CreateFactor("Inflation Pressure", "Inflation", "Tracks whether trend inflation is running hotter than neutral.", 0.22m, false, "Core CPI Trend", "BLS/FRED", "% y/y", 2.5m, 0.5m, 3.1m),
        CreateFactor("Inflation Breadth", "Inflation", "Measures how broadly price pressure is distributed across components.", 0.18m, false, "Trimmed Mean CPI", "Cleveland Fed", "% y/y", 2.4m, 0.4m, 2.9m),
        CreateFactor("Energy Shock", "Commodities", "Captures oil and energy pressure that can spill into inflation expectations.", 0.15m, false, "WTI Crude Oil", "EIA", "USD/bbl", 75m, 12m, 88m),
        CreateFactor("Growth Stress", "Growth", "Summarizes downside stress in activity and labor-market momentum.", 0.20m, true, "ISM Manufacturing PMI", "ISM", "Index", 50m, 2m, 48.6m),
        CreateFactor("Fiscal/Treasury Stress", "Rates", "Monitors Treasury-market and fiscal financing stress.", 0.13m, false, "10Y Treasury Term Premium", "NY Fed", "%", 0.25m, 0.35m, 0.72m),
        CreateFactor("Market Complacency", "Markets", "Identifies whether risk pricing appears too relaxed versus macro risk.", 0.12m, false, "VIX Index", "Cboe", "Index", 18m, 5m, 14.2m)
    ];

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
