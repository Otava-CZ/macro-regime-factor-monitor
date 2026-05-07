using Microsoft.EntityFrameworkCore;

namespace MacroRegimeFactorMonitor.Data;

public static class DatabaseSchema
{
    private static readonly IReadOnlyList<ColumnDefinition> TradeIdeaColumns =
    [
        new("EntryTrigger", "TEXT", "''"),
        new("Invalidation", "TEXT", "''"),
        new("Catalyst", "TEXT", "''"),
        new("MaxLoss", "TEXT", "''"),
        new("TimeHorizon", "TEXT", "''"),
        new("PostMortem", "TEXT", "''")
    ];

    public static async Task UpgradeAsync(MacroRegimeDbContext db)
    {
        var existingColumns = await GetColumnNamesAsync(db, "TradeIdeas");

        foreach (var column in TradeIdeaColumns)
        {
            if (existingColumns.Contains(column.Name))
            {
                continue;
            }

            await db.Database.ExecuteSqlRawAsync(
                $"ALTER TABLE TradeIdeas ADD COLUMN {column.Name} {column.SqlType} NOT NULL DEFAULT {column.DefaultValue};");
        }
    }

    private static async Task<HashSet<string>> GetColumnNamesAsync(MacroRegimeDbContext db, string tableName)
    {
        var connection = db.Database.GetDbConnection();
        var shouldClose = connection.State == System.Data.ConnectionState.Closed;

        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"PRAGMA table_info({tableName});";

            await using var reader = await command.ExecuteReaderAsync();
            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            while (await reader.ReadAsync())
            {
                columns.Add(reader.GetString(1));
            }

            return columns;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private sealed record ColumnDefinition(string Name, string SqlType, string DefaultValue);
}
