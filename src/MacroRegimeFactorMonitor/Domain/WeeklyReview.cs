using System.ComponentModel.DataAnnotations;

namespace MacroRegimeFactorMonitor.Domain;

public sealed class WeeklyReview
{
    public int Id { get; set; }
    public DateOnly WeekEnding { get; set; }
    [Required]
    public required string RegimeAssessment { get; set; }
    public string KeyDevelopments { get; set; } = string.Empty;
    public string RisksToWatch { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
