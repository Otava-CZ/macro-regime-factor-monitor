using System.ComponentModel.DataAnnotations;

namespace MacroRegimeFactorMonitor.Domain;

public sealed class TradeIdea
{
    public int Id { get; set; }
    public DateOnly IdeaDate { get; set; }
    [Required]
    public required string Title { get; set; }
    public string Thesis { get; set; } = string.Empty;
    public string Instrument { get; set; } = string.Empty;
    public string Status { get; set; } = "Watching";
    public string RiskNotes { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
