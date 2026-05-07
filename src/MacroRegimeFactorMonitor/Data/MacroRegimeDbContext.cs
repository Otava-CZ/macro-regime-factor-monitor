using MacroRegimeFactorMonitor.Domain;
using Microsoft.EntityFrameworkCore;

namespace MacroRegimeFactorMonitor.Data;

public sealed class MacroRegimeDbContext(DbContextOptions<MacroRegimeDbContext> options) : DbContext(options)
{
    public DbSet<MacroFactor> MacroFactors => Set<MacroFactor>();
    public DbSet<Indicator> Indicators => Set<Indicator>();
    public DbSet<IndicatorObservation> IndicatorObservations => Set<IndicatorObservation>();
    public DbSet<FactorScore> FactorScores => Set<FactorScore>();
    public DbSet<WeeklyReview> WeeklyReviews => Set<WeeklyReview>();
    public DbSet<TradeIdea> TradeIdeas => Set<TradeIdea>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MacroFactor>(entity =>
        {
            entity.Property(factor => factor.Name).HasMaxLength(120).IsRequired();
            entity.Property(factor => factor.Category).HasMaxLength(80).IsRequired();
            entity.Property(factor => factor.Weight).HasPrecision(8, 4);
            entity.HasIndex(factor => factor.Name).IsUnique();
        });

        modelBuilder.Entity<Indicator>(entity =>
        {
            entity.Property(indicator => indicator.Name).HasMaxLength(120).IsRequired();
            entity.Property(indicator => indicator.Source).HasMaxLength(80);
            entity.Property(indicator => indicator.Unit).HasMaxLength(40);
            entity.Property(indicator => indicator.Baseline).HasPrecision(12, 4);
            entity.Property(indicator => indicator.Volatility).HasPrecision(12, 4);
            entity.HasOne(indicator => indicator.MacroFactor)
                .WithMany(factor => factor.Indicators)
                .HasForeignKey(indicator => indicator.MacroFactorId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<IndicatorObservation>(entity =>
        {
            entity.Property(observation => observation.Value).HasPrecision(12, 4);
            entity.HasIndex(observation => new { observation.IndicatorId, observation.ObservationDate }).IsUnique();
            entity.HasOne(observation => observation.Indicator)
                .WithMany(indicator => indicator.Observations)
                .HasForeignKey(observation => observation.IndicatorId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<FactorScore>(entity =>
        {
            entity.Property(score => score.RawScore).HasPrecision(8, 4);
            entity.Property(score => score.WeightedScore).HasPrecision(8, 4);
            entity.Property(score => score.RegimeImpact).HasMaxLength(80).IsRequired();
            entity.HasIndex(score => new { score.MacroFactorId, score.ScoreDate }).IsUnique();
            entity.HasOne(score => score.MacroFactor)
                .WithMany(factor => factor.Scores)
                .HasForeignKey(score => score.MacroFactorId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<WeeklyReview>(entity =>
        {
            entity.Property(review => review.RegimeAssessment).HasMaxLength(120).IsRequired();
            entity.HasIndex(review => review.WeekEnding).IsUnique();
        });

        modelBuilder.Entity<TradeIdea>(entity =>
        {
            entity.Property(idea => idea.Title).HasMaxLength(160).IsRequired();
            entity.Property(idea => idea.Instrument).HasMaxLength(80);
            entity.Property(idea => idea.Status).HasMaxLength(40);
            entity.Property(idea => idea.MaxLoss).HasMaxLength(120);
            entity.Property(idea => idea.TimeHorizon).HasMaxLength(120);
        });
    }
}
