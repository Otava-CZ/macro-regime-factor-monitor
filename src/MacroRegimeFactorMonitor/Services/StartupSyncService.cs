using MacroRegimeFactorMonitor.Data;
using MacroRegimeFactorMonitor.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MacroRegimeFactorMonitor.Services;

public sealed class StartupSyncService(
    IDbContextFactory<MacroRegimeDbContext> dbFactory,
    ILogger<StartupSyncService> logger)
{
    public async Task RunAsync()
    {
        var startedAtUtc = DateTime.UtcNow;
        await using var db = await dbFactory.CreateDbContextAsync();
        DatabaseSeedResult? seedResult = null;
        var appliedMigrations = string.Empty;

        try
        {
            logger.LogInformation("Applying startup migrations/schema upgrades.");
            await db.ApplyStartupSchemaUpgradesAsync();
            appliedMigrations = await GetAppliedMigrationsMessageAsync(db);

            seedResult = await DatabaseSeeder.SeedAsync(db, logger);

            db.StartupSyncRuns.Add(CreateRun(startedAtUtc, "Completed", appliedMigrations, seedResult));
            await db.SaveChangesAsync();

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
        catch (Exception ex)
        {
            logger.LogError(ex, "Startup sync failed.");
            await TryWriteFailedRunAsync(startedAtUtc, appliedMigrations, seedResult, ex);
            throw;
        }
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
            failureDb.StartupSyncRuns.Add(CreateRun(
                startedAtUtc,
                "Failed",
                appliedMigrations,
                seedResult,
                exception.Message));
            await failureDb.SaveChangesAsync();
        }
        catch (Exception auditException)
        {
            logger.LogWarning(auditException, "Unable to write failed startup sync audit row.");
        }
    }
}
