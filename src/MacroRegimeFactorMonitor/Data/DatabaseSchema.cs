using Microsoft.EntityFrameworkCore;

namespace MacroRegimeFactorMonitor.Data;

public static class DatabaseSchema
{
    private static readonly IReadOnlyDictionary<string, string> TradeIdeaColumns = new Dictionary<string, string>
    {
        ["EntryTrigger"] = "TEXT NOT NULL DEFAULT ''",
        ["Invalidation"] = "TEXT NOT NULL DEFAULT ''",
        ["Catalyst"] = "TEXT NOT NULL DEFAULT ''",
        ["MaxLoss"] = "TEXT NOT NULL DEFAULT ''",
        ["TimeHorizon"] = "TEXT NOT NULL DEFAULT ''",
        ["PostMortem"] = "TEXT NOT NULL DEFAULT ''"
    };

    public static async Task EnsureCreatedAndUpgradedAsync(MacroRegimeDbContext db)
    {
        await db.Database.EnsureCreatedAsync();

        if (!db.Database.IsSqlite())
        {
            return;
        }

        var existingColumns = await GetTradeIdeaColumnsAsync(db);
        foreach (var (columnName, definition) in TradeIdeaColumns)
        {
            if (existingColumns.Contains(columnName))
            {
                continue;
            }

            await db.Database.ExecuteSqlRawAsync($"ALTER TABLE TradeIdeas ADD COLUMN {columnName} {definition};");
        }
    }

    private static async Task<HashSet<string>> GetTradeIdeaColumnsAsync(MacroRegimeDbContext db)
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
            command.CommandText = "PRAGMA table_info('TradeIdeas');";
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
}
