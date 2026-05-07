namespace MacroRegimeFactorMonitor.Services.Imports;

public sealed class DataSourceClientFactory(IEnumerable<IDataSourceClient> clients) : IDataSourceClientFactory
{
    private readonly IReadOnlyDictionary<string, IDataSourceClient> _clients = clients.ToDictionary(
        client => client.SourceName,
        StringComparer.OrdinalIgnoreCase);

    public IDataSourceClient GetClient(string sourceName)
    {
        if (_clients.TryGetValue(sourceName, out var client))
        {
            return client;
        }

        throw new InvalidOperationException($"No import client is registered for data source '{sourceName}'. Supported sources are: FRED, BLS, EIA, Treasury Fiscal Data.");
    }
}
