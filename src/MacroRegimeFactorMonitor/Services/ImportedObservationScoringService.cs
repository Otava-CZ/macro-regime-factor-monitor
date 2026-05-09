using MacroRegimeFactorMonitor.Data;
using MacroRegimeFactorMonitor.Domain;
using Microsoft.EntityFrameworkCore;

namespace MacroRegimeFactorMonitor.Services;

public sealed class ImportedObservationScoringService(IDbContextFactory<MacroRegimeDbContext> dbFactory)
{
    public const string ImportedManualDataMode = "ImportedManual";
    public const string ScoringModelVersion = "imported-manual-v0.8.2";

    private static readonly Dictionary<string, ImportedObservationScoringRule> RulesByFactorName = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Inflation Pressure"] = new(
            "CPILFESL",
            "CPILFESL YoY",
            "Monthly"),
        ["Fiscal/Treasury Stress"] = new(
            "DGS10",
            "DGS10 Treasury-rate proxy",
            "Daily"),
        ["Market Complacency"] = new(
            "VIXCLS",
            "VIXCLS",
            "Daily")
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
                    : CreateUnmappedPlaceholder();

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
                    db.FactorScores.Add(CreateFactorScore(factor.Id, scoreDate, calculation, calculatedAtUtc));
                    result.ScoresInserted++;
                }
                else
                {
                    ApplyCalculation(existingScore, calculation, calculatedAtUtc);
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
                    calculation.WindowObservationCount,
                    calculation.WindowStartDate,
                    calculation.WindowEndDate,
                    calculation.WindowChange,
                    calculation.WindowChangePercent,
                    calculation.WindowSlope,
                    calculation.WindowAcceleration,
                    calculation.ScoringConfidence,
                    calculation.ScoringConfidenceNotes,
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

        var versions = await db.FactorScores
            .AsNoTracking()
            .Where(score => score.ScoreDate == latestDate && score.DataMode == ImportedManualDataMode)
            .Select(score => score.ScoringModelVersion ?? string.Empty)
            .Distinct()
            .ToListAsync(cancellationToken);
        var preferredVersion = SelectPreferredImportedManualVersion(versions);

        return await db.FactorScores
            .AsNoTracking()
            .Include(score => score.MacroFactor)
            .Where(score => score.ScoreDate == latestDate
                && score.DataMode == ImportedManualDataMode
                && score.ScoringModelVersion == preferredVersion)
            .OrderBy(score => score.MacroFactor!.Name)
            .ToListAsync(cancellationToken);
    }

    public static string SelectPreferredImportedManualVersion(IEnumerable<string?> versions)
    {
        var versionList = versions
            .Where(version => !string.IsNullOrWhiteSpace(version))
            .Select(version => version!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return versionList.FirstOrDefault(version => string.Equals(version, ImportedObservationScoringService.ScoringModelVersion, StringComparison.OrdinalIgnoreCase))
            ?? versionList.OrderByDescending(version => version, StringComparer.OrdinalIgnoreCase).FirstOrDefault()
            ?? string.Empty;
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
                && item.ExternalSeriesId != null
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
                CalculationNotes = $"No imported {rule.ExternalSeriesId} observation available on or before ScoreDate {scoreDate:yyyy-MM-dd}. RawScore = 0 because no imported observation was available. DataQualityStatus = Missing. ScoringConfidence = Missing.",
                SourceObservationCount = 0,
                WindowObservationCount = 0,
                DataQualityStatus = "Missing",
                DataQualityNotes = missingDataQualityNotes,
                ScoringConfidence = "Missing",
                ScoringConfidenceNotes = missingDataQualityNotes
            };
        }

        var previousObservation = await db.IndicatorObservations
            .AsNoTracking()
            .Include(item => item.ExternalSeries)
            .Where(item => item.ObservationDate < observation.ObservationDate
                && item.ExternalSeriesId != null
                && item.ExternalSeries != null
                && item.ExternalSeries.DataSource != null
                && item.ExternalSeries.DataSource.Name == "FRED"
                && item.ExternalSeries.ExternalSeriesId == rule.ExternalSeriesId)
            .OrderByDescending(item => item.ObservationDate)
            .ThenByDescending(item => item.UpdatedAtUtc ?? item.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var windowStartDate = GetLookbackStartDate(rule.SeriesFrequency, observation.ObservationDate);
        var windowObservations = await db.IndicatorObservations
            .AsNoTracking()
            .Include(item => item.ExternalSeries)
            .Where(item => item.ObservationDate >= windowStartDate
                && item.ObservationDate <= observation.ObservationDate
                && item.ExternalSeriesId != null
                && item.ExternalSeries != null
                && item.ExternalSeries.DataSource != null
                && item.ExternalSeries.DataSource.Name == "FRED"
                && item.ExternalSeries.ExternalSeriesId == rule.ExternalSeriesId)
            .OrderBy(item => item.ObservationDate)
            .ThenBy(item => item.UpdatedAtUtc ?? item.CreatedAtUtc)
            .ToListAsync(cancellationToken);
        var window = BuildWindowDiagnostics(windowObservations);

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
        var baseScore = CalculateBaseScore(rule.ExternalSeriesId, observation.Value);
        var adjustment = CalculateWindowAdjustment(rule.ExternalSeriesId, window.WindowChange);
        var rawScore = Clamp(baseScore + adjustment);
        var confidence = DetermineScoringConfidence(rule.SeriesFrequency, dataQualityStatus, window.WindowObservationCount);
        var confidenceNotes = BuildScoringConfidenceNotes(confidence, rule.SeriesFrequency, dataQualityStatus, window.WindowObservationCount);
        var notes = BuildMappedNotes(
            rule.ExternalSeriesId,
            factor.Name,
            observation.ObservationDate,
            observation.Value,
            previousObservation?.ObservationDate,
            previousObservation?.Value,
            observationChange,
            daysSinceSourceObservation,
            dataQualityStatus,
            window,
            baseScore,
            adjustment,
            rawScore,
            confidence,
            confidenceNotes);

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
            DataQualityNotes = dataQualityNotes,
            WindowObservationCount = window.WindowObservationCount,
            WindowStartDate = window.WindowStartDate,
            WindowEndDate = window.WindowEndDate,
            WindowMinValue = window.WindowMinValue,
            WindowMaxValue = window.WindowMaxValue,
            WindowAverageValue = window.WindowAverageValue,
            WindowFirstValue = window.WindowFirstValue,
            WindowLastValue = window.WindowLastValue,
            WindowChange = window.WindowChange,
            WindowChangePercent = window.WindowChangePercent,
            WindowSlope = window.WindowSlope,
            WindowAcceleration = window.WindowAcceleration,
            ScoringConfidence = confidence,
            ScoringConfidenceNotes = confidenceNotes
        };
    }

    private static FactorScore CreateFactorScore(
        int macroFactorId,
        DateOnly scoreDate,
        ImportedObservationScoreCalculation calculation,
        DateTime calculatedAtUtc)
    {
        var score = new FactorScore
        {
            MacroFactorId = macroFactorId,
            ScoreDate = scoreDate,
            RawScore = calculation.RawScore,
            WeightedScore = calculation.WeightedScore,
            RegimeImpact = calculation.RegimeImpact,
            Notes = calculation.Notes,
            DataMode = ImportedManualDataMode,
            ScoringModelVersion = ImportedObservationScoringService.ScoringModelVersion
        };
        ApplyCalculation(score, calculation, calculatedAtUtc);
        return score;
    }

    private static void ApplyCalculation(
        FactorScore score,
        ImportedObservationScoreCalculation calculation,
        DateTime calculatedAtUtc)
    {
        score.RawScore = calculation.RawScore;
        score.WeightedScore = calculation.WeightedScore;
        score.RegimeImpact = calculation.RegimeImpact;
        score.Notes = calculation.Notes;
        score.SourceObservationCount = calculation.SourceObservationCount;
        score.SourceObservationDate = calculation.SourceObservationDate;
        score.PreviousObservationDate = calculation.PreviousObservationDate;
        score.SourceObservationValue = calculation.SourceObservationValue;
        score.PreviousObservationValue = calculation.PreviousObservationValue;
        score.ObservationChange = calculation.ObservationChange;
        score.ObservationChangePercent = calculation.ObservationChangePercent;
        score.DaysSinceSourceObservation = calculation.DaysSinceSourceObservation;
        score.DataQualityStatus = calculation.DataQualityStatus;
        score.DataQualityNotes = calculation.DataQualityNotes;
        score.WindowObservationCount = calculation.WindowObservationCount;
        score.WindowStartDate = calculation.WindowStartDate;
        score.WindowEndDate = calculation.WindowEndDate;
        score.WindowMinValue = calculation.WindowMinValue;
        score.WindowMaxValue = calculation.WindowMaxValue;
        score.WindowAverageValue = calculation.WindowAverageValue;
        score.WindowFirstValue = calculation.WindowFirstValue;
        score.WindowLastValue = calculation.WindowLastValue;
        score.WindowChange = calculation.WindowChange;
        score.WindowChangePercent = calculation.WindowChangePercent;
        score.WindowSlope = calculation.WindowSlope;
        score.WindowAcceleration = calculation.WindowAcceleration;
        score.ScoringConfidence = calculation.ScoringConfidence;
        score.ScoringConfidenceNotes = calculation.ScoringConfidenceNotes;
        score.CalculatedAtUtc = calculatedAtUtc;
        score.CalculationNotes = calculation.CalculationNotes;
    }

    private static ImportedObservationScoreCalculation CreateUnmappedPlaceholder()
    {
        const string notes = "No imported observation mapping available yet; neutral placeholder score.";
        return new ImportedObservationScoreCalculation
        {
            RawScore = 0m,
            WeightedScore = 0m,
            RegimeImpact = "Neutral / unavailable imported data",
            Notes = notes,
            CalculationNotes = $"{notes} SourceObservationCount = 0. WindowObservationCount = 0. DataQualityStatus = Placeholder. ScoringConfidence = Placeholder.",
            SourceObservationCount = 0,
            WindowObservationCount = 0,
            DataQualityStatus = "Placeholder",
            DataQualityNotes = "No imported observation mapping available yet.",
            ScoringConfidence = "Placeholder",
            ScoringConfidenceNotes = "No imported observation mapping available yet."
        };
    }

    private static ObservationWindowDiagnostics BuildWindowDiagnostics(IReadOnlyList<IndicatorObservation> observations)
    {
        if (observations.Count == 0)
        {
            return new ObservationWindowDiagnostics { WindowObservationCount = 0 };
        }

        var first = observations[0];
        var last = observations[^1];
        var change = last.Value - first.Value;
        var values = observations.Select(observation => observation.Value).ToList();
        var firstHalfSlope = CalculateHalfSlope(observations.Take(observations.Count / 2).ToList());
        var secondHalfSlope = CalculateHalfSlope(observations.Skip(observations.Count / 2).ToList());

        return new ObservationWindowDiagnostics
        {
            WindowObservationCount = observations.Count,
            WindowStartDate = first.ObservationDate,
            WindowEndDate = last.ObservationDate,
            WindowMinValue = values.Min(),
            WindowMaxValue = values.Max(),
            WindowAverageValue = values.Average(),
            WindowFirstValue = first.Value,
            WindowLastValue = last.Value,
            WindowChange = change,
            WindowChangePercent = first.Value != 0m ? change / Math.Abs(first.Value) * 100m : null,
            WindowSlope = observations.Count >= 2 ? change / (observations.Count - 1) : null,
            WindowAcceleration = observations.Count >= 4 ? secondHalfSlope - firstHalfSlope : null
        };
    }

    private static decimal CalculateHalfSlope(IReadOnlyList<IndicatorObservation> observations)
    {
        if (observations.Count == 0)
        {
            return 0m;
        }

        return (observations[^1].Value - observations[0].Value) / Math.Max(1, observations.Count - 1);
    }

    private static DateOnly GetLookbackStartDate(string seriesFrequency, DateOnly sourceObservationDate) => seriesFrequency switch
    {
        "Monthly" => sourceObservationDate.AddMonths(-12),
        "Daily" => sourceObservationDate.AddDays(-60),
        _ => sourceObservationDate.AddDays(-90)
    };

    private static decimal CalculateBaseScore(string seriesId, decimal value) => seriesId switch
    {
        "CPILFESL" => value switch
        {
            <= 2.5m => -1.0m,
            <= 3.5m => 0.0m,
            <= 4.5m => 1.0m,
            _ => 2.0m
        },
        "DGS10" => value switch
        {
            < 3.5m => -0.5m,
            < 4.5m => 0.5m,
            < 5.0m => 1.0m,
            _ => 2.0m
        },
        "VIXCLS" => value switch
        {
            < 13m => 2.0m,
            < 16m => 1.0m,
            < 22m => 0.0m,
            < 30m => -1.0m,
            _ => -2.0m
        },
        _ => 0m
    };

    private static decimal CalculateWindowAdjustment(string seriesId, decimal? windowChange) => (seriesId, windowChange) switch
    {
        (_, null) => 0m,
        ("CPILFESL", >= 0.75m) => 0.5m,
        ("CPILFESL", <= -0.75m) => -0.5m,
        ("DGS10", >= 0.50m) => 0.5m,
        ("DGS10", <= -0.50m) => -0.5m,
        ("VIXCLS", <= -5.0m) => 0.5m,
        ("VIXCLS", >= 5.0m) => -0.5m,
        _ => 0m
    };

    private static string DetermineScoringConfidence(string seriesFrequency, string dataQualityStatus, int windowObservationCount)
    {
        var highThreshold = seriesFrequency switch
        {
            "Monthly" => 8,
            "Daily" => 30,
            _ => int.MaxValue
        };

        if (dataQualityStatus == "Fresh" && windowObservationCount >= highThreshold)
        {
            return "High";
        }

        if ((dataQualityStatus == "Fresh" || dataQualityStatus == "Stale") && windowObservationCount >= 3)
        {
            return "Medium";
        }

        return "Low";
    }

    private static string BuildScoringConfidenceNotes(string confidence, string seriesFrequency, string dataQualityStatus, int windowObservationCount)
    {
        var cadence = seriesFrequency.Equals("Daily", StringComparison.OrdinalIgnoreCase) ? "daily" : seriesFrequency.Equals("Monthly", StringComparison.OrdinalIgnoreCase) ? "monthly" : "unknown-frequency";
        var windowDescription = seriesFrequency switch
        {
            "Monthly" => "12-month window",
            "Daily" => "60-day window",
            _ => "90-day window"
        };
        var freshness = dataQualityStatus.Equals("Fresh", StringComparison.OrdinalIgnoreCase) ? "fresh" : "stale";

        return confidence switch
        {
            "High" => $"High confidence: {freshness} {cadence} data and {windowObservationCount} observations in the {windowDescription}.",
            "Medium" => $"Medium confidence: {freshness} {cadence} data but {windowObservationCount} observations available in the {windowDescription}.",
            _ => $"Low confidence: only {windowObservationCount} observations available in the lookback window."
        };
    }

    private static string BuildMappedNotes(
        string seriesId,
        string factorName,
        DateOnly observationDate,
        decimal value,
        DateOnly? previousObservationDate,
        decimal? previousValue,
        decimal? observationChange,
        int daysSinceSourceObservation,
        string dataQualityStatus,
        ObservationWindowDiagnostics window,
        decimal baseScore,
        decimal adjustment,
        decimal rawScore,
        string scoringConfidence,
        string scoringConfidenceNotes)
    {
        var unitSuffix = seriesId is "CPILFESL" or "DGS10" ? "%" : string.Empty;
        var latestText = seriesId == "CPILFESL"
            ? $"CPILFESL YoY latest observation {observationDate:yyyy-MM-dd} = {FormatObservationValue(value)}{unitSuffix}; base score {FormatSignedObservationValue(baseScore)}."
            : $"{seriesId} latest observation {observationDate:yyyy-MM-dd} = {FormatObservationValue(value)}{unitSuffix}; base score {FormatSignedObservationValue(baseScore)}.";
        var previousText = previousObservationDate is not null && previousValue is not null
            ? $" Previous observation {previousObservationDate:yyyy-MM-dd} = {FormatObservationValue(previousValue.Value)}{unitSuffix}; one-step change {FormatSignedNullableObservationValue(observationChange)}."
            : " Previous observation unavailable; one-step change unavailable.";
        var windowText = window.WindowObservationCount > 0
            ? $" Window {window.WindowStartDate:yyyy-MM-dd} to {window.WindowEndDate:yyyy-MM-dd} has {window.WindowObservationCount} observations; window change {FormatSignedNullableObservationValue(window.WindowChange)} ({FormatSignedNullableObservationValue(window.WindowChangePercent)}%); slope {FormatSignedNullableObservationValue(window.WindowSlope)}; acceleration {FormatSignedNullableObservationValue(window.WindowAcceleration)}."
            : " Window diagnostics unavailable; window observation count 0.";
        var adjustmentText = $" Window adjustment {FormatSignedObservationValue(adjustment)}; final RawScore {FormatSignedObservationValue(rawScore)} after clamping to [-2, +2].";
        var qualityText = $" Data age = {daysSinceSourceObservation} days; DataQualityStatus = {dataQualityStatus}; ScoringConfidence = {scoringConfidence}. {scoringConfidenceNotes}";
        var caveat = seriesId switch
        {
            "CPILFESL" => " Rule: latest value <=2.5 negative pressure, 2.5-3.5 balanced, 3.5-4.5 elevated, >4.5 high; 12-month change of +/-0.75 adjusts by +/-0.5.",
            "DGS10" => " Rule: latest level <3.5 relief, 3.5-4.5 mild stress, 4.5-5 elevated stress, >=5 high stress; 60-day change of +/-0.50 adjusts by +/-0.5. DGS10 is a rough Treasury-rate proxy, not true fiscal stress or term premium.",
            "VIXCLS" => " Rule: <13 high complacency pressure, 13-16 elevated complacency pressure, 16-22 balanced, 22-30 reduced complacency, >=30 negative complacency score; falling/rising 60-day VIX by 5 adjusts by +/-0.5. Low VIX increases Market Complacency pressure; high VIX reduces complacency pressure.",
            _ => $" Rule: imported manual placeholder rule for mapped source series. Factor = {factorName}."
        };

        return $"{latestText}{previousText}{windowText}{adjustmentText}{qualityText}{caveat}";
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

    private static string FormatSignedNullableObservationValue(decimal? value) => value?.ToString("+0.##;-0.##;0") ?? "unavailable";

    private static decimal Clamp(decimal value) => Math.Min(2m, Math.Max(-2m, value));

    private sealed record ImportedObservationScoringRule(
        string ExternalSeriesId,
        string Description,
        string SeriesFrequency);

    private class ObservationWindowDiagnostics
    {
        public int WindowObservationCount { get; set; }
        public DateOnly? WindowStartDate { get; set; }
        public DateOnly? WindowEndDate { get; set; }
        public decimal? WindowMinValue { get; set; }
        public decimal? WindowMaxValue { get; set; }
        public decimal? WindowAverageValue { get; set; }
        public decimal? WindowFirstValue { get; set; }
        public decimal? WindowLastValue { get; set; }
        public decimal? WindowChange { get; set; }
        public decimal? WindowChangePercent { get; set; }
        public decimal? WindowSlope { get; set; }
        public decimal? WindowAcceleration { get; set; }
    }

    private sealed class ImportedObservationScoreCalculation : ObservationWindowDiagnostics
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
        public string? ScoringConfidence { get; set; }
        public string? ScoringConfidenceNotes { get; set; }
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
    int? WindowObservationCount,
    DateOnly? WindowStartDate,
    DateOnly? WindowEndDate,
    decimal? WindowChange,
    decimal? WindowChangePercent,
    decimal? WindowSlope,
    decimal? WindowAcceleration,
    string? ScoringConfidence,
    string? ScoringConfidenceNotes,
    string CalculationNotes);
