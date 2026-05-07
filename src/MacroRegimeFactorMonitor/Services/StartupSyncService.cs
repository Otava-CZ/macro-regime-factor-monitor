using MacroRegimeFactorMonitor.Data;
using MacroRegimeFactorMonitor.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MacroRegimeFactorMonitor.Services;

public sealed class StartupSyncService(
    IDbContextFactory<MacroRegimeDbContext> dbFactory,
    ILogger<StartupSyncService> logger)
{
    private const string CompletedStatus = "Completed";
    private const string FailedStatus = "Failed";

    public async Task RunAsync()
    {
        var startedAtUtc = DateTime.UtcNow;
        await using var db = await dbFactory.CreateDbContextAsync();
        DatabaseSeedResult? seedResult = null;
        var appliedMigrations = string.Empty;

        try
        {
            appliedMigrations = await ApplySchemaUpgradesAsync(db);
            seedResult = await SeedDatabaseAsync(db);

            await WriteRunAsync(db, startedAtUtc, CompletedStatus, appliedMigrations, seedResult);
            LogSeedSummary(seedResult);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Startup sync failed.");
            await TryWriteFailedRunAsync(startedAtUtc, appliedMigrations, seedResult, ex);
            throw;
        }
    }

    private async Task<string> ApplySchemaUpgradesAsync(MacroRegimeDbContext db)
    {
        logger.LogInformation("Applying startup migrations/schema upgrades.");
        await db.ApplyStartupSchemaUpgradesAsync();
        return await GetAppliedMigrationsMessageAsync(db);
    }

    private async Task<DatabaseSeedResult> SeedDatabaseAsync(MacroRegimeDbContext db)
    {
        // Seeding is additive/guarded so repeated startup syncs do not duplicate the sample dataset.
        return await DatabaseSeeder.SeedAsync(db, logger);
    }

    private static async Task WriteRunAsync(
        MacroRegimeDbContext db,
        DateTime startedAtUtc,
        string status,
        string appliedMigrations,
        DatabaseSeedResult? seedResult,
        string errorMessage = "")
    {
        db.StartupSyncRuns.Add(CreateRun(startedAtUtc, status, appliedMigrations, seedResult, errorMessage));
        await db.SaveChangesAsync();
    }

    private void LogSeedSummary(DatabaseSeedResult seedResult)
    {
        logger.LogInformation(
            "Startup sync completed. DataSources={SeededDataSources}, MacroFactors={SeededMacroFactors}, Indicators={SeededIndicators}, Observations={SeededObservations}, FactorScores={SeededFactorScores}, WeeklyReviews={SeededWeeklyReviews}, TradeIdeas={SeededTradeIdeas}.",
            seedResult.SeededDataSources,
            seedResult.SeededMacroFactors,
            seedResult.SeededIndicators,
            seedResult.SeededObservations,
            seedResult.SeededFactorScores,
            seedResult.SeededWeeklyReviews,
            seedResult.SeededTradeIdeas);
    }

    private static StartupSyncRun CreateRun(
        DateTime startedAtUtc,
        string status,
        string appliedMigrations,
        DatabaseSeedResult? seedResult,
        string errorMessage = "") =>
        new()
        {
            StartedAtUtc = startedAtUtc,
            FinishedAtUtc = DateTime.UtcNow,
            Status = status,
            Message = seedResult?.Message ?? string.Empty,
            AppliedMigrations = appliedMigrations,
            SeededDataSources = seedResult?.SeededDataSources ?? 0,
            SeededMacroFactors = seedResult?.SeededMacroFactors ?? 0,
            SeededIndicators = seedResult?.SeededIndicators ?? 0,
            SeededObservations = seedResult?.SeededObservations ?? 0,
            SeededFactorScores = seedResult?.SeededFactorScores ?? 0,
            SeededWeeklyReviews = seedResult?.SeededWeeklyReviews ?? 0,
            SeededTradeIdeas = seedResult?.SeededTradeIdeas ?? 0,
            ErrorMessage = errorMessage
        };

    private static async Task<string> GetAppliedMigrationsMessageAsync(MacroRegimeDbContext db)
    {
        if (!db.Database.IsNpgsql())
        {
            return string.Empty;
        }

        var migrations = await db.Database.GetAppliedMigrationsAsync();
        return string.Join(",", migrations);
    }

    private async Task TryWriteFailedRunAsync(
        DateTime startedAtUtc,
        string appliedMigrations,
        DatabaseSeedResult? seedResult,
        Exception exception)
    {
        try
        {
            await using var failureDb = await dbFactory.CreateDbContextAsync();
            await WriteRunAsync(
                failureDb,
                startedAtUtc,
                FailedStatus,
                appliedMigrations,
                seedResult,
                exception.Message);
        }
        catch (Exception auditException)
        {
            logger.LogWarning(auditException, "Unable to write failed startup sync audit row.");
        }
    }
}
