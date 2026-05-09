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
                        SourceObservationDate = calculation.SourceObservationDate,
                        PreviousObservationDate = calculation.PreviousObservationDate,
                        SourceObservationValue = calculation.SourceObservationValue,
                        PreviousObservationValue = calculation.PreviousObservationValue,
                        ObservationChange = calculation.ObservationChange,
                        ObservationChangePercent = calculation.ObservationChangePercent,
                        DaysSinceSourceObservation = calculation.DaysSinceSourceObservation,
                        DataQualityStatus = calculation.DataQualityStatus,
                        DataQualityNotes = calculation.DataQualityNotes,
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
                    existingScore.SourceObservationDate = calculation.SourceObservationDate;
                    existingScore.PreviousObservationDate = calculation.PreviousObservationDate;
                    existingScore.SourceObservationValue = calculation.SourceObservationValue;
                    existingScore.PreviousObservationValue = calculation.PreviousObservationValue;
                    existingScore.ObservationChange = calculation.ObservationChange;
                    existingScore.ObservationChangePercent = calculation.ObservationChangePercent;
                    existingScore.DaysSinceSourceObservation = calculation.DaysSinceSourceObservation;
                    existingScore.DataQualityStatus = calculation.DataQualityStatus;
                    existingScore.DataQualityNotes = calculation.DataQualityNotes;
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
                    calculation.SourceObservationDate,
                    calculation.SourceObservationValue,
                    calculation.PreviousObservationDate,
                    calculation.PreviousObservationValue,
                    calculation.ObservationChange,
                    calculation.DaysSinceSourceObservation,
                    calculation.DataQualityStatus,
                    calculation.DataQualityNotes,
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
            const string missingDataQualityNotes = "No imported observation available on or before ScoreDate.";
            return new ImportedObservationScoreCalculation
            {
                RawScore = 0m,
                Notes = $"No imported {rule.ExternalSeriesId} observation available on or before {scoreDate:yyyy-MM-dd}; neutral missing-data score.",
                CalculationNotes = $"No imported {rule.ExternalSeriesId} observation available on or before ScoreDate {scoreDate:yyyy-MM-dd}. RawScore = 0 because no imported observation was available.",
                SourceObservationCount = 0,
                DataQualityStatus = "Missing",
                DataQualityNotes = missingDataQualityNotes
            };
        }

        var previousObservation = await db.IndicatorObservations
            .AsNoTracking()
            .Include(item => item.ExternalSeries)
            .Where(item => item.ObservationDate < observation.ObservationDate
                && item.ExternalSeries != null
                && item.ExternalSeries.DataSource != null
                && item.ExternalSeries.DataSource.Name == "FRED"
                && item.ExternalSeries.ExternalSeriesId == rule.ExternalSeriesId)
            .OrderByDescending(item => item.ObservationDate)
            .ThenByDescending(item => item.UpdatedAtUtc ?? item.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var daysSinceSourceObservation = scoreDate.DayNumber - observation.ObservationDate.DayNumber;
        var dataQualityStatus = DetermineDataQualityStatus(observation.ExternalSeries?.Frequency, daysSinceSourceObservation);
        var dataQualityNotes = dataQualityStatus == "Stale"
            ? $"Source data is stale: latest {rule.ExternalSeriesId} observation is {daysSinceSourceObservation} days before ScoreDate."
            : $"Source data is fresh: latest {rule.ExternalSeriesId} observation is {daysSinceSourceObservation} days before ScoreDate.";
        var observationChange = previousObservation is null
            ? (decimal?)null
            : observation.Value - previousObservation.Value;
        var observationChangePercent = previousObservation is not null && previousObservation.Value != 0m && observationChange is not null
            ? observationChange.Value / Math.Abs(previousObservation.Value) * 100m
            : (decimal?)null;
        var rawScore = rule.Score(observation.Value);
        var notes = BuildMappedNotes(
            rule.ExternalSeriesId,
            observation.ObservationDate,
            observation.Value,
            previousObservation?.ObservationDate,
            previousObservation?.Value,
            observationChange,
            daysSinceSourceObservation,
            dataQualityStatus,
            rawScore,
            factor.Name);

        return new ImportedObservationScoreCalculation
        {
            RawScore = rawScore,
            Notes = notes,
            CalculationNotes = notes,
            SourceObservationCount = 1,
            SourceObservationDate = observation.ObservationDate,
            PreviousObservationDate = previousObservation?.ObservationDate,
            SourceObservationValue = observation.Value,
            PreviousObservationValue = previousObservation?.Value,
            ObservationChange = observationChange,
            ObservationChangePercent = observationChangePercent,
            DaysSinceSourceObservation = daysSinceSourceObservation,
            DataQualityStatus = dataQualityStatus,
            DataQualityNotes = dataQualityNotes
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
            SourceObservationCount = 0,
            DataQualityStatus = "Placeholder",
            DataQualityNotes = "No imported observation mapping available yet."
        };
    }

    private static string BuildMappedNotes(
        string seriesId,
        DateOnly observationDate,
        decimal value,
        DateOnly? previousObservationDate,
        decimal? previousValue,
        decimal? observationChange,
        int daysSinceSourceObservation,
        string dataQualityStatus,
        decimal rawScore,
        string factorName)
    {
        var unitSuffix = seriesId is "CPILFESL" or "DGS10" ? "%" : string.Empty;
        var latestText = seriesId == "CPILFESL"
            ? $"CPILFESL YoY latest observation {observationDate:yyyy-MM-dd} = {FormatObservationValue(value)}{unitSuffix}."
            : $"{seriesId} latest observation {observationDate:yyyy-MM-dd} = {FormatObservationValue(value)}{unitSuffix}.";
        var previousText = previousObservationDate is not null && previousValue is not null
            ? $" Previous {previousObservationDate:yyyy-MM-dd} = {FormatObservationValue(previousValue.Value)}{unitSuffix}."
            : " Previous observation unavailable.";
        var changeText = observationChange is not null
            ? $" Change = {FormatSignedObservationValue(observationChange.Value)}."
            : " Change unavailable.";
        var diagnosticsText = $" Data age = {daysSinceSourceObservation} days, status {dataQualityStatus}. RawScore = {FormatSignedObservationValue(rawScore)}.";

        return seriesId switch
        {
            "CPILFESL" => $"{latestText}{previousText}{changeText}{diagnosticsText} Rule: <=2.5 negative pressure, 2.5-3.5 balanced, 3.5-4.5 elevated, >4.5 high.",
            "DGS10" => $"{latestText}{previousText}{changeText}{diagnosticsText} Rule: <3.5 relief, 3.5-4.5 mild stress, 4.5-5 elevated stress, >=5 high stress. DGS10 is a rough Treasury-rate proxy, not true fiscal stress or term premium.",
            "VIXCLS" => $"{latestText}{previousText}{changeText}{diagnosticsText} Rule: <13 high complacency pressure, 13-16 elevated complacency pressure, 16-22 balanced, 22-30 reduced complacency, >=30 negative complacency score. Low VIX increases Market Complacency pressure; high VIX reduces complacency pressure.",
            _ => $"{latestText} Factor = {factorName}.{previousText}{changeText}{diagnosticsText} Rule: imported manual placeholder rule for mapped source series."
        };
    }

    private static string DetermineDataQualityStatus(string? frequency, int daysSinceSourceObservation)
    {
        var freshThreshold = frequency?.Trim().ToUpperInvariant() switch
        {
            "D" or "DAILY" => 5,
            "M" or "MONTHLY" => 45,
            _ => 14
        };

        return daysSinceSourceObservation <= freshThreshold ? "Fresh" : "Stale";
    }

    private static string FormatObservationValue(decimal value) => value.ToString("0.##");

    private static string FormatSignedObservationValue(decimal value) => value.ToString("+0.##;-0.##;0");

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
        public DateOnly? SourceObservationDate { get; set; }
        public DateOnly? PreviousObservationDate { get; set; }
        public decimal? SourceObservationValue { get; set; }
        public decimal? PreviousObservationValue { get; set; }
        public decimal? ObservationChange { get; set; }
        public decimal? ObservationChangePercent { get; set; }
        public int? DaysSinceSourceObservation { get; set; }
        public string? DataQualityStatus { get; set; }
        public string? DataQualityNotes { get; set; }
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
    DateOnly? SourceObservationDate,
    decimal? SourceObservationValue,
    DateOnly? PreviousObservationDate,
    decimal? PreviousObservationValue,
    decimal? ObservationChange,
    int? DaysSinceSourceObservation,
    string? DataQualityStatus,
    string? DataQualityNotes,
    string CalculationNotes);
