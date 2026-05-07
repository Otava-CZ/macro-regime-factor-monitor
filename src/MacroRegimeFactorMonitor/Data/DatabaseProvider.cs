using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace MacroRegimeFactorMonitor.Data;

public static class DatabaseProvider
{
    public const string ConnectionStringName = "MacroRegime";
    public const string DefaultSqliteConnectionString = "Data Source=macro-regime.db";

    public static bool IsPostgres(IConfiguration configuration) =>
        string.Equals(configuration["Database:Provider"], "Postgres", StringComparison.OrdinalIgnoreCase);

    public static string GetConnectionString(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName);

        return string.IsNullOrWhiteSpace(connectionString)
            ? DefaultSqliteConnectionString
            : connectionString;
    }

    public static DbContextOptionsBuilder UseConfiguredDatabase(
        this DbContextOptionsBuilder options,
        IConfiguration configuration)
    {
        var connectionString = GetConnectionString(configuration);

        return IsPostgres(configuration)
            ? options.UseNpgsql(connectionString)
            : options.UseSqlite(connectionString);
    }
}
