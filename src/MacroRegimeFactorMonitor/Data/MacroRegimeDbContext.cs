using System.Data;
using MacroRegimeFactorMonitor.Domain;
using Microsoft.EntityFrameworkCore;

namespace MacroRegimeFactorMonitor.Data;

public sealed class MacroRegimeDbContext(DbContextOptions<MacroRegimeDbContext> options) : DbContext(options)
{
    public DbSet<MacroFactor> MacroFactors => Set<MacroFactor>();
    public DbSet<Indicator> Indicators => Set<Indicator>();
    public DbSet<IndicatorObservation> IndicatorObservations => Set<IndicatorObservation>();
    public DbSet<DataSource> DataSources => Set<DataSource>();
    public DbSet<ExternalSeries> ExternalSeries => Set<ExternalSeries>();
    public DbSet<DataImportRun> DataImportRuns => Set<DataImportRun>();
    public DbSet<FactorScore> FactorScores => Set<FactorScore>();
    public DbSet<WeeklyReview> WeeklyReviews => Set<WeeklyReview>();
    public DbSet<TradeIdea> TradeIdeas => Set<TradeIdea>();
    public DbSet<StartupSyncRun> StartupSyncRuns => Set<StartupSyncRun>();

    public async Task ApplyStartupSchemaUpgradesAsync()
    {
        if (Database.IsNpgsql())
        {
            await Database.MigrateAsync();
            return;
        }

        await Database.EnsureCreatedAsync();

        if (Database.IsSqlite())
        {
            await ApplySqliteSchemaUpgradesAsync();
        }
    }

    private async Task ApplySqliteSchemaUpgradesAsync()
    {
        await EnsureSqliteStartupSyncRunsTableAsync();

        var existingColumns = await GetTableColumnsAsync("TradeIdeas");
        if (existingColumns.Count == 0)
        {
            return;
        }

        var v03Columns = new Dictionary<string, string>
        {
            [nameof(TradeIdea.EntryTrigger)] = "TEXT NOT NULL DEFAULT ''",
            [nameof(TradeIdea.Invalidation)] = "TEXT NOT NULL DEFAULT ''",
            [nameof(TradeIdea.Catalyst)] = "TEXT NOT NULL DEFAULT ''",
            [nameof(TradeIdea.MaxLoss)] = "TEXT NOT NULL DEFAULT ''",
            [nameof(TradeIdea.TimeHorizon)] = "TEXT NOT NULL DEFAULT ''",
            [nameof(TradeIdea.PostMortem)] = "TEXT NOT NULL DEFAULT ''"
        };

        foreach (var (columnName, columnDefinition) in v03Columns)
        {
            if (existingColumns.Contains(columnName))
            {
                continue;
            }

            await Database.ExecuteSqlRawAsync($"ALTER TABLE TradeIdeas ADD COLUMN {columnName} {columnDefinition};");
        }
    }


    private async Task EnsureSqliteStartupSyncRunsTableAsync()
    {
        await Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS StartupSyncRuns (
                Id INTEGER NOT NULL CONSTRAINT PK_StartupSyncRuns PRIMARY KEY AUTOINCREMENT,
                StartedAtUtc TEXT NOT NULL,
                FinishedAtUtc TEXT NULL,
                Status TEXT NOT NULL,
                Message TEXT NOT NULL DEFAULT '',
                AppliedMigrations TEXT NOT NULL DEFAULT '',
                SeededDataSources INTEGER NOT NULL DEFAULT 0,
                SeededMacroFactors INTEGER NOT NULL DEFAULT 0,
                SeededIndicators INTEGER NOT NULL DEFAULT 0,
                SeededObservations INTEGER NOT NULL DEFAULT 0,
                SeededFactorScores INTEGER NOT NULL DEFAULT 0,
                SeededWeeklyReviews INTEGER NOT NULL DEFAULT 0,
                SeededTradeIdeas INTEGER NOT NULL DEFAULT 0,
                ErrorMessage TEXT NOT NULL DEFAULT ''
            );
            """);

        await Database.ExecuteSqlRawAsync("""
            CREATE INDEX IF NOT EXISTS IX_StartupSyncRuns_StartedAtUtc
            ON StartupSyncRuns (StartedAtUtc);
            """);

        await Database.ExecuteSqlRawAsync("""
            CREATE INDEX IF NOT EXISTS IX_StartupSyncRuns_Status
            ON StartupSyncRuns (Status);
            """);
    }

    private async Task<HashSet<string>> GetTableColumnsAsync(string tableName)
    {
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var connection = Database.GetDbConnection();

        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info('{tableName.Replace("'", "''")}');";
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            columns.Add(reader.GetString(1));
        }

        return columns;
    }

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

        modelBuilder.Entity<DataSource>(entity =>
        {
            entity.Property(source => source.Name).HasMaxLength(120).IsRequired();
            entity.Property(source => source.SourceType).HasMaxLength(60).IsRequired();
            entity.Property(source => source.BaseUrl).HasMaxLength(500).IsRequired();
            entity.Property(source => source.Notes).HasDefaultValue(string.Empty);
            entity.Property(source => source.IsActive).HasDefaultValue(true);
        });

        modelBuilder.Entity<ExternalSeries>(entity =>
        {
            entity.Property(series => series.ExternalSeriesId).HasMaxLength(120).IsRequired();
            entity.Property(series => series.Endpoint).HasMaxLength(500).IsRequired();
            entity.Property(series => series.Frequency).HasMaxLength(40).IsRequired();
            entity.Property(series => series.Units).HasMaxLength(80).IsRequired();
            entity.Property(series => series.Transform).HasMaxLength(80).IsRequired();
            entity.Property(series => series.ObservationDateField).HasMaxLength(80).IsRequired();
            entity.Property(series => series.ValueField).HasMaxLength(80).IsRequired();
            entity.Property(series => series.Notes).HasDefaultValue(string.Empty);
            entity.Property(series => series.IsActive).HasDefaultValue(true);
            entity.HasIndex(series => new { series.DataSourceId, series.ExternalSeriesId, series.IndicatorId }).IsUnique();
            entity.HasIndex(series => series.IndicatorId);
            entity.HasIndex(series => series.DataSourceId);
            entity.HasOne(series => series.Indicator)
                .WithMany(indicator => indicator.ExternalSeries)
                .HasForeignKey(series => series.IndicatorId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(series => series.DataSource)
                .WithMany(source => source.ExternalSeries)
                .HasForeignKey(series => series.DataSourceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DataImportRun>(entity =>
        {
            entity.Property(run => run.Status).HasMaxLength(40).IsRequired();
            entity.Property(run => run.ErrorMessage).HasDefaultValue(string.Empty);
            entity.Property(run => run.Notes).HasDefaultValue(string.Empty);
            entity.HasIndex(run => new { run.DataSourceId, run.StartedAtUtc });
            entity.HasIndex(run => run.Status);
            entity.HasOne(run => run.DataSource)
                .WithMany(source => source.ImportRuns)
                .HasForeignKey(run => run.DataSourceId)
                .OnDelete(DeleteBehavior.Cascade);
        });


        modelBuilder.Entity<StartupSyncRun>(entity =>
        {
            entity.Property(run => run.StartedAtUtc).IsRequired();
            entity.Property(run => run.Status).HasMaxLength(40).IsRequired();
            entity.Property(run => run.Message).HasDefaultValue(string.Empty);
            entity.Property(run => run.AppliedMigrations).HasDefaultValue(string.Empty);
            entity.Property(run => run.ErrorMessage).HasDefaultValue(string.Empty);
            entity.HasIndex(run => run.StartedAtUtc);
            entity.HasIndex(run => run.Status);
        });

        modelBuilder.Entity<IndicatorObservation>(entity =>
        {
            entity.Property(observation => observation.Value).HasPrecision(12, 4);
            entity.Property(observation => observation.Source).HasMaxLength(120).HasDefaultValue(string.Empty);
            // TODO: replace this with provider-compatible vintage-aware uniqueness once NULL
            // VintageDate behavior is handled consistently across PostgreSQL and SQLite.
            entity.HasIndex(observation => new { observation.IndicatorId, observation.ObservationDate }).IsUnique();
            entity.HasOne(observation => observation.Indicator)
                .WithMany(indicator => indicator.Observations)
                .HasForeignKey(observation => observation.IndicatorId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(observation => observation.ExternalSeries)
                .WithMany(series => series.Observations)
                .HasForeignKey(observation => observation.ExternalSeriesId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(observation => observation.DataImportRun)
                .WithMany(run => run.Observations)
                .HasForeignKey(observation => observation.DataImportRunId)
                .OnDelete(DeleteBehavior.SetNull);
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
            entity.Property(idea => idea.EntryTrigger).HasMaxLength(500);
            entity.Property(idea => idea.Invalidation).HasMaxLength(500);
            entity.Property(idea => idea.Catalyst).HasMaxLength(500);
            entity.Property(idea => idea.MaxLoss).HasMaxLength(120);
            entity.Property(idea => idea.TimeHorizon).HasMaxLength(120);
            entity.Property(idea => idea.PostMortem).HasMaxLength(1000);
        });
    }
}
