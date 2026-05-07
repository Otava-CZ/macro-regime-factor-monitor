namespace MacroRegimeFactorMonitor.Data;

public sealed class DatabaseSeedResult
{
    public int SeededDataSources { get; set; }
    public int SeededMacroFactors { get; set; }
    public int SeededIndicators { get; set; }
    public int SeededObservations { get; set; }
    public int SeededFactorScores { get; set; }
    public int SeededWeeklyReviews { get; set; }
    public int SeededTradeIdeas { get; set; }
    public string Message { get; set; } = string.Empty;
}
