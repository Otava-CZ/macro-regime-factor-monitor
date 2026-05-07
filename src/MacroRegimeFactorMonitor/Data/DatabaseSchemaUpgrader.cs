using Microsoft.EntityFrameworkCore;

namespace MacroRegimeFactorMonitor.Data;

public static class DatabaseSchemaUpgrader
{
    private static readonly IReadOnlyList<SqliteColumnUpgrade> TradeIdeaColumns =
    [
        new("MacroRegime", "TEXT NOT NULL DEFAULT ''"),
        new("PressureThesis", "TEXT NOT NULL DEFAULT ''"),
        new("TimeHorizon", "TEXT NOT NULL DEFAULT ''"),
        new("EntryTrigger", "TEXT NOT NULL DEFAULT ''"),
        new("ExitPlan", "TEXT NOT NULL DEFAULT ''")
    ];

    public static async Task UpgradeAsync(MacroRegimeDbContext db)
    {
        if (!await TableExistsAsync(db, "TradeIdeas"))
        {
            return;
        }

        var existingColumns = await GetColumnsAsync(db, "TradeIdeas");
        foreach (var column in TradeIdeaColumns.Where(column => !existingColumns.Contains(column.Name)))
        {
            await db.Database.ExecuteSqlRawAsync($"ALTER TABLE TradeIdeas ADD COLUMN {column.Name} {column.Definition};");
        }
    }

    private static async Task<bool> TableExistsAsync(MacroRegimeDbContext db, string tableName)
    {
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name = $tableName;";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "$tableName";
        parameter.Value = tableName;
        command.Parameters.Add(parameter);

        if (command.Connection?.State != System.Data.ConnectionState.Open)
        {
            await db.Database.OpenConnectionAsync();
        }

        var result = await command.ExecuteScalarAsync();
        return result is not null;
    }

    private static async Task<HashSet<string>> GetColumnsAsync(MacroRegimeDbContext db, string tableName)
    {
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = $"PRAGMA table_info({tableName});";

        if (command.Connection?.State != System.Data.ConnectionState.Open)
        {
            await db.Database.OpenConnectionAsync();
        }

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            columns.Add(reader.GetString(1));
        }

        return columns;
    }

    private sealed record SqliteColumnUpgrade(string Name, string Definition);
}
