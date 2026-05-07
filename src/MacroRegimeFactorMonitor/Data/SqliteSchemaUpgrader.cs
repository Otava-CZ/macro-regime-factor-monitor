using Microsoft.EntityFrameworkCore;

namespace MacroRegimeFactorMonitor.Data;

public static class SqliteSchemaUpgrader
{
    private static readonly string[] TradeIdeaJournalColumns =
    [
        "EntryTrigger",
        "Invalidation",
        "Catalyst",
        "MaxLoss",
        "TimeHorizon",
        "PostMortem"
    ];

    public static async Task UpgradeAsync(MacroRegimeDbContext db)
    {
        if (!db.Database.IsSqlite())
        {
            return;
        }

        var existingColumns = await GetTableColumnsAsync(db, "TradeIdeas");
        if (existingColumns.Count == 0)
        {
            return;
        }

        foreach (var column in TradeIdeaJournalColumns.Except(existingColumns, StringComparer.OrdinalIgnoreCase))
        {
            await db.Database.ExecuteSqlRawAsync($"ALTER TABLE TradeIdeas ADD COLUMN {column} TEXT NOT NULL DEFAULT '';");
        }
    }

    private static async Task<HashSet<string>> GetTableColumnsAsync(MacroRegimeDbContext db, string tableName)
    {
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var connection = db.Database.GetDbConnection();
        var shouldClose = connection.State != System.Data.ConnectionState.Open;

        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"PRAGMA table_info('{tableName}');";

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
