using Microsoft.EntityFrameworkCore;

namespace MacroRegimeFactorMonitor.Data;

public static class DatabaseSchema
{
    private static readonly IReadOnlyList<ColumnDefinition> TradeIdeaColumns =
    [
        new("EntryTrigger", "TEXT NOT NULL DEFAULT ''"),
        new("Invalidation", "TEXT NOT NULL DEFAULT ''"),
        new("Catalyst", "TEXT NOT NULL DEFAULT ''"),
        new("MaxLoss", "TEXT NULL"),
        new("TimeHorizon", "TEXT NOT NULL DEFAULT ''"),
        new("PostMortem", "TEXT NOT NULL DEFAULT ''")
    ];

    public static async Task UpgradeAsync(MacroRegimeDbContext db)
    {
        await db.Database.EnsureCreatedAsync();
        var existingTradeIdeaColumns = await GetColumnsAsync(db, "TradeIdeas");

        foreach (var column in TradeIdeaColumns)
        {
            if (existingTradeIdeaColumns.Contains(column.Name))
            {
                continue;
            }

            await db.Database.ExecuteSqlRawAsync($"ALTER TABLE TradeIdeas ADD COLUMN {column.Name} {column.SqlDefinition};");
        }
    }

    private static async Task<HashSet<string>> GetColumnsAsync(MacroRegimeDbContext db, string tableName)
    {
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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

            while (await reader.ReadAsync())
            {
                columns.Add(reader.GetString(1));
            }
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }

        return columns;
    }

    private sealed record ColumnDefinition(string Name, string SqlDefinition);
}
