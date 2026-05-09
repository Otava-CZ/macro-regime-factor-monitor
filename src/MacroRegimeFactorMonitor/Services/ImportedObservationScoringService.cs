using MacroRegimeFactorMonitor.Data;
using MacroRegimeFactorMonitor.Domain;
using Microsoft.EntityFrameworkCore;

namespace MacroRegimeFactorMonitor.Services;

public sealed class ImportedObservationScoringService(IDbContextFactory<MacroRegimeDbContext> dbFactory)
{
    public const string ImportedManualDataMode = "ImportedManual";
    public const string ScoringModelVersion = "imported-manual-v0.8.0";

    private static readonly Dictionary<string, ImportedObservationScoringRule> RulesByFactorName = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Inflation Pressure"] = new(
            "CPILFESL",
            ScoreCoreCpi,
            "CPILFESL YoY"),
        ["Fiscal/Treasury Stress"] = new(
            "DGS10",
            ScoreDgs10,
            "DGS10 Treasury-rate proxy"),
        ["Market Complacency"] = new(
            "VIXCLS",
            ScoreVix,
            "VIXCLS")
    };

    public async Task<ImportedObservationScoringResult> RecalculateCurrentScoresAsync(
        DateOnly scoreDate,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var calculatedAtUtc = DateTime.UtcNow;
        var result = new ImportedObservationScoringResult
        {
            ScoreDate = scoreDate,
            DataMode = ImportedManualDataMode,
            ScoringModelVersion = ImportedObservationScoringService.ScoringModelVersion,
            CalculatedAtUtc = calculatedAtUtc
        };

        var factors = await db.MacroFactors
            .OrderBy(factor => factor.Name)
            .ToListAsync(cancellationToken);

        foreach (var factor in factors)
        {
            try
            {
                var calculation = RulesByFactorName.TryGetValue(factor.Name, out var rule)
                    ? await CalculateMappedScoreAsync(db, factor, rule, scoreDate, cancellationToken)
                    : CreateUnmappedPlaceholder(factor);

                calculation.RawScore = Clamp(calculation.RawScore);
                calculation.WeightedScore = Math.Round(calculation.RawScore * factor.Weight, 2);
                calculation.RegimeImpact = calculation.SourceObservationCount == 0 && !RulesByFactorName.ContainsKey(factor.Name)
                    ? "Neutral / unavailable imported data"
                    : FactorScoreCalculator.ClassifyPressureImpact(calculation.WeightedScore, factor.Name);

                var existingScore = await db.FactorScores
                    .FirstOrDefaultAsync(score => score.MacroFactorId == factor.Id
                        && score.ScoreDate == scoreDate
                        && score.DataMode == ImportedManualDataMode
                        && score.ScoringModelVersion == ImportedObservationScoringService.ScoringModelVersion,
                        cancellationToken);

                if (existingScore is null)
                {
                    db.FactorScores.Add(new FactorScore
                    {
                        MacroFactorId = factor.Id,
                        ScoreDate = scoreDate,
                        RawScore = calculation.RawScore,
                        WeightedScore = calculation.WeightedScore,
                        RegimeImpact = calculation.RegimeImpact,
                        Notes = calculation.Notes,
                        DataMode = ImportedManualDataMode,
                        ScoringModelVersion = ImportedObservationScoringService.ScoringModelVersion,
                        SourceObservationCount = calculation.SourceObservationCount,
                        CalculatedAtUtc = calculatedAtUtc,
                        CalculationNotes = calculation.CalculationNotes
                    });
                    result.ScoresInserted++;
                }
                else
                {
                    existingScore.RawScore = calculation.RawScore;
                    existingScore.WeightedScore = calculation.WeightedScore;
                    existingScore.RegimeImpact = calculation.RegimeImpact;
                    existingScore.Notes = calculation.Notes;
                    existingScore.SourceObservationCount = calculation.SourceObservationCount;
                    existingScore.CalculatedAtUtc = calculatedAtUtc;
                    existingScore.CalculationNotes = calculation.CalculationNotes;
                    result.ScoresUpdated++;
                }

                result.ScoreRows.Add(new ImportedObservationScoreRow(
                    factor.Name,
                    calculation.RawScore,
                    calculation.WeightedScore,
                    calculation.RegimeImpact,
                    calculation.SourceObservationCount,
                    calculation.CalculationNotes));
            }
            catch (Exception exception)
            {
                result.ScoresFailed++;
                result.Warnings.Add($"{factor.Name}: {exception.Message}");
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return result;
    }

    public async Task<IReadOnlyList<FactorScore>> GetLatestImportedManualScoresAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var latestDate = await db.FactorScores
            .AsNoTracking()
            .Where(score => score.DataMode == ImportedManualDataMode)
            .MaxAsync(score => (DateOnly?)score.ScoreDate, cancellationToken);

        if (latestDate is null)
        {
            return [];
        }

        return await db.FactorScores
            .AsNoTracking()
            .Include(score => score.MacroFactor)
            .Where(score => score.ScoreDate == latestDate
                && score.DataMode == ImportedManualDataMode)
            .OrderBy(score => score.MacroFactor!.Name)
            .ToListAsync(cancellationToken);
    }

    private static async Task<ImportedObservationScoreCalculation> CalculateMappedScoreAsync(
        MacroRegimeDbContext db,
        MacroFactor factor,
        ImportedObservationScoringRule rule,
        DateOnly scoreDate,
        CancellationToken cancellationToken)
    {
        var observation = await db.IndicatorObservations
            .AsNoTracking()
            .Include(item => item.ExternalSeries)
            .Where(item => item.ObservationDate <= scoreDate
                && item.ExternalSeries != null
                && item.ExternalSeries.DataSource != null
                && item.ExternalSeries.DataSource.Name == "FRED"
                && item.ExternalSeries.ExternalSeriesId == rule.ExternalSeriesId)
            .OrderByDescending(item => item.ObservationDate)
            .ThenByDescending(item => item.UpdatedAtUtc ?? item.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (observation is null)
        {
            return new ImportedObservationScoreCalculation
            {
                RawScore = 0m,
                Notes = $"No imported {rule.ExternalSeriesId} observation available on or before {scoreDate:yyyy-MM-dd}; neutral placeholder score.",
                CalculationNotes = $"No imported {rule.ExternalSeriesId} observation available on or before {scoreDate:yyyy-MM-dd}; neutral placeholder score.",
                SourceObservationCount = 0
            };
        }

        var rawScore = rule.Score(observation.Value);
        var notes = BuildMappedNotes(rule.ExternalSeriesId, observation.ObservationDate, observation.Value, factor.Name);
        return new ImportedObservationScoreCalculation
        {
            RawScore = rawScore,
            Notes = notes,
            CalculationNotes = notes,
            SourceObservationCount = 1
        };
    }

    private static ImportedObservationScoreCalculation CreateUnmappedPlaceholder(MacroFactor factor)
    {
        const string notes = "No imported observation mapping available yet; neutral placeholder score.";
        return new ImportedObservationScoreCalculation
        {
            RawScore = 0m,
            WeightedScore = 0m,
            RegimeImpact = "Neutral / unavailable imported data",
            Notes = notes,
            CalculationNotes = notes,
            SourceObservationCount = 0
        };
    }

    private static string BuildMappedNotes(string seriesId, DateOnly observationDate, decimal value, string factorName)
    {
        var formattedValue = value.ToString("0.##");
        return seriesId switch
        {
            "CPILFESL" => $"CPILFESL YoY latest observation {observationDate:yyyy-MM-dd} = {formattedValue}%.",
            "DGS10" => $"DGS10 latest observation {observationDate:yyyy-MM-dd} = {formattedValue}%. DGS10 is a rough Treasury-rate proxy, not true fiscal stress or term premium.",
            "VIXCLS" => $"VIXCLS latest observation {observationDate:yyyy-MM-dd} = {formattedValue}. Low VIX increases Market Complacency pressure; high VIX reduces complacency pressure.",
            _ => $"{seriesId} latest observation {observationDate:yyyy-MM-dd} = {formattedValue} for {factorName}."
        };
    }

    private static decimal ScoreCoreCpi(decimal value) => value switch
    {
        <= 2.5m => -1.0m,
        <= 3.5m => 0.0m,
        <= 4.5m => 1.0m,
        _ => 2.0m
    };

    private static decimal ScoreDgs10(decimal value) => value switch
    {
        < 3.5m => -0.5m,
        < 4.5m => 0.5m,
        < 5.0m => 1.0m,
        _ => 2.0m
    };

    private static decimal ScoreVix(decimal value) => value switch
    {
        < 13m => 2.0m,
        < 16m => 1.0m,
        < 22m => 0.0m,
        < 30m => -1.0m,
        _ => -2.0m
    };

    private static decimal Clamp(decimal value) => Math.Min(2m, Math.Max(-2m, value));

    private sealed record ImportedObservationScoringRule(
        string ExternalSeriesId,
        Func<decimal, decimal> Score,
        string Description);

    private sealed class ImportedObservationScoreCalculation
    {
        public decimal RawScore { get; set; }
        public decimal WeightedScore { get; set; }
        public string RegimeImpact { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        public string CalculationNotes { get; set; } = string.Empty;
        public int SourceObservationCount { get; set; }
    }
}

public sealed class ImportedObservationScoringResult
{
    public DateOnly ScoreDate { get; set; }
    public string DataMode { get; set; } = string.Empty;
    public string ScoringModelVersion { get; set; } = string.Empty;
    public DateTime CalculatedAtUtc { get; set; }
    public int ScoresInserted { get; set; }
    public int ScoresUpdated { get; set; }
    public int ScoresFailed { get; set; }
    public int ScoresSkipped { get; set; }
    public List<string> Warnings { get; } = [];
    public List<ImportedObservationScoreRow> ScoreRows { get; } = [];
}

public sealed record ImportedObservationScoreRow(
    string MacroFactor,
    decimal RawScore,
    decimal WeightedScore,
    string RegimeImpact,
    int SourceObservationCount,
    string CalculationNotes);
