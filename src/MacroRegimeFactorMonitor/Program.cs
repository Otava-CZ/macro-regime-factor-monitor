using System.Globalization;
using System.Text;

namespace MacroRegimeFactorMonitor;

public static class Program
{
    public static int Main(string[] args)
    {
        var dataPath = ResolveDataPath(args);
        if (!File.Exists(dataPath))
        {
            Console.Error.WriteLine($"Factor input file was not found: {dataPath}");
            return 1;
        }

        var observations = FactorObservation.LoadFromCsv(dataPath);
        if (observations.Count == 0)
        {
            Console.Error.WriteLine($"Factor input file did not contain any observations: {dataPath}");
            return 1;
        }

        var report = RegimeMonitor.Evaluate(observations);
        Console.WriteLine(ReportFormatter.ToMarkdown(report));
        return 0;
    }

    private static string ResolveDataPath(string[] args)
    {
        if (args.Length > 0)
        {
            return Path.GetFullPath(args[0]);
        }

        var repositorySample = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "data",
            "sample-factors.csv"));
        return File.Exists(repositorySample)
            ? repositorySample
            : Path.Combine(AppContext.BaseDirectory, "data", "sample-factors.csv");
    }
}

public sealed record FactorObservation(
    DateOnly Date,
    string Factor,
    string Category,
    decimal Value,
    decimal Baseline,
    decimal Volatility,
    bool HigherIsRiskOn,
    decimal Weight)
{
    public static IReadOnlyList<FactorObservation> LoadFromCsv(string path)
    {
        var observations = new List<FactorObservation>();
        foreach (var line in File.ReadLines(path).Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var columns = line.Split(',', StringSplitOptions.TrimEntries);
            if (columns.Length != 8)
            {
                throw new InvalidDataException($"Expected 8 columns but found {columns.Length}: {line}");
            }

            observations.Add(new FactorObservation(
                DateOnly.Parse(columns[0], CultureInfo.InvariantCulture),
                columns[1],
                columns[2],
                decimal.Parse(columns[3], CultureInfo.InvariantCulture),
                decimal.Parse(columns[4], CultureInfo.InvariantCulture),
                decimal.Parse(columns[5], CultureInfo.InvariantCulture),
                bool.Parse(columns[6]),
                decimal.Parse(columns[7], CultureInfo.InvariantCulture)));
        }

        return observations;
    }
}

public sealed record FactorScore(
    FactorObservation Observation,
    decimal ZScore,
    decimal WeightedContribution);

public sealed record CategoryScore(
    string Category,
    decimal Score,
    IReadOnlyList<FactorScore> Factors);

public sealed record RegimeReport(
    DateOnly AsOfDate,
    string Regime,
    decimal CompositeScore,
    string Summary,
    IReadOnlyList<CategoryScore> Categories,
    IReadOnlyList<FactorScore> Factors);

public static class RegimeMonitor
{
    public static RegimeReport Evaluate(IReadOnlyList<FactorObservation> observations)
    {
        var asOfDate = observations.Max(observation => observation.Date);
        var latestObservations = observations
            .Where(observation => observation.Date == asOfDate)
            .ToList();

        var factors = latestObservations
            .Select(ToScore)
            .OrderByDescending(score => Math.Abs(score.WeightedContribution))
            .ToList();

        var categories = factors
            .GroupBy(score => score.Observation.Category)
            .Select(group => new CategoryScore(
                group.Key,
                Math.Round(group.Sum(score => score.WeightedContribution), 2),
                group.OrderByDescending(score => Math.Abs(score.WeightedContribution)).ToList()))
            .OrderByDescending(category => Math.Abs(category.Score))
            .ToList();

        var compositeScore = Math.Round(factors.Sum(score => score.WeightedContribution), 2);
        var regime = Classify(compositeScore);
        return new RegimeReport(
            asOfDate,
            regime,
            compositeScore,
            BuildSummary(regime, compositeScore, categories),
            categories,
            factors);
    }

    private static FactorScore ToScore(FactorObservation observation)
    {
        if (observation.Volatility <= 0)
        {
            throw new InvalidDataException($"Volatility must be positive for factor '{observation.Factor}'.");
        }

        var direction = observation.HigherIsRiskOn ? 1 : -1;
        var zScore = Math.Round(((observation.Value - observation.Baseline) / observation.Volatility) * direction, 2);
        var contribution = Math.Round(zScore * observation.Weight, 2);
        return new FactorScore(observation, zScore, contribution);
    }

    private static string Classify(decimal compositeScore) => compositeScore switch
    {
        >= 1.5m => "Expansion / Risk-On",
        >= 0.4m => "Constructive Growth",
        <= -1.5m => "Contraction / Risk-Off",
        <= -0.4m => "Defensive Slowdown",
        _ => "Neutral / Transition"
    };

    private static string BuildSummary(string regime, decimal compositeScore, IReadOnlyList<CategoryScore> categories)
    {
        var strongestCategory = categories.FirstOrDefault();
        if (strongestCategory is null)
        {
            return $"The monitor classifies the current regime as {regime} with a composite score of {compositeScore}.";
        }

        var direction = strongestCategory.Score >= 0 ? "supporting" : "pressuring";
        return $"The monitor classifies the current regime as {regime} with a composite score of {compositeScore}. " +
               $"{strongestCategory.Category} is the largest category signal, {direction} the composite by " +
               $"{Math.Abs(strongestCategory.Score)} points.";
    }
}

public static class ReportFormatter
{
    public static string ToMarkdown(RegimeReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Macro Regime Factor Monitor");
        builder.AppendLine();
        builder.AppendLine($"As of: {report.AsOfDate:yyyy-MM-dd}");
        builder.AppendLine($"Regime: **{report.Regime}**");
        builder.AppendLine($"Composite score: **{report.CompositeScore}**");
        builder.AppendLine();
        builder.AppendLine(report.Summary);
        builder.AppendLine();
        builder.AppendLine("## Category Signals");
        builder.AppendLine();
        builder.AppendLine("| Category | Score |");
        builder.AppendLine("| --- | ---: |");
        foreach (var category in report.Categories)
        {
            builder.AppendLine($"| {category.Category} | {category.Score} |");
        }

        builder.AppendLine();
        builder.AppendLine("## Factor Detail");
        builder.AppendLine();
        builder.AppendLine("| Factor | Category | Value | Baseline | Z-score | Contribution |");
        builder.AppendLine("| --- | --- | ---: | ---: | ---: | ---: |");
        foreach (var factor in report.Factors)
        {
            builder.AppendLine(
                $"| {factor.Observation.Factor} | {factor.Observation.Category} | " +
                $"{factor.Observation.Value} | {factor.Observation.Baseline} | " +
                $"{factor.ZScore} | {factor.WeightedContribution} |");
        }

        return builder.ToString();
    }
}
