namespace MacroRegimeFactorMonitor.Domain;

public sealed class StartupSyncRun
{
    public int Id { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public DateTime? FinishedAtUtc { get; set; }
    public required string Status { get; set; }
    public string Message { get; set; } = string.Empty;
    public string AppliedMigrations { get; set; } = string.Empty;
    public int SeededDataSources { get; set; }
    public int SeededMacroFactors { get; set; }
    public int SeededIndicators { get; set; }
    public int SeededObservations { get; set; }
    public int SeededFactorScores { get; set; }
    public int SeededWeeklyReviews { get; set; }
    public int SeededTradeIdeas { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
}
