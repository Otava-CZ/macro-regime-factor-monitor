using System.Globalization;
using System.Net;
using System.Text.Json;
using MacroRegimeFactorMonitor.Domain;
using Microsoft.AspNetCore.WebUtilities;

namespace MacroRegimeFactorMonitor.Services.Imports;

public sealed class FredDataSourceClient(
    HttpClient httpClient,
    IConfiguration configuration,
    ILogger<FredDataSourceClient> logger) : IDataSourceClient
{
    private const string DefaultBaseUrl = "https://api.stlouisfed.org/fred";
    private const string ApiKeyConfigurationKey = "Fred:ApiKey";
    private const string BaseUrlConfigurationKey = "Fred:BaseUrl";
    private const string ObservationsEndpoint = "/series/observations";
    private const string Source = "FRED";

    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);

    public string SourceName => Source;

    public async Task<IReadOnlyList<ImportObservationDto>> FetchObservationsAsync(
        ExternalSeries externalSeries,
        DateOnly? fromDate,
        DateOnly? toDate,
        CancellationToken cancellationToken = default)
    {
        var apiKey = configuration[ApiKeyConfigurationKey];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("Fred:ApiKey is missing. Store it with dotnet user-secrets or environment variables.");
        }

        var requestUri = BuildRequestUri(externalSeries, apiKey, fromDate, toDate);
        using var response = await httpClient.GetAsync(requestUri, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"FRED observations request failed with status {(int)response.StatusCode} ({response.StatusCode}). Response: {CreateSafeResponseSummary(responseBody, apiKey)}");
        }

        FredObservationsResponse? fredResponse;
        try
        {
            fredResponse = JsonSerializer.Deserialize<FredObservationsResponse>(responseBody, JsonSerializerOptions);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException("FRED observations response could not be parsed as JSON.", exception);
        }

        if (fredResponse?.Observations is null)
        {
            return [];
        }

        var observations = new List<ImportObservationDto>();
        foreach (var observation in fredResponse.Observations)
        {
            if (!TryParseObservation(observation, externalSeries.ExternalSeriesId, out var importObservation))
            {
                continue;
            }

            observations.Add(importObservation);
        }

        return observations;
    }

    private Uri BuildRequestUri(
        ExternalSeries externalSeries,
        string apiKey,
        DateOnly? fromDate,
        DateOnly? toDate)
    {
        if (!string.Equals(externalSeries.Endpoint, ObservationsEndpoint, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"FRED external series {externalSeries.ExternalSeriesId} uses unsupported endpoint '{externalSeries.Endpoint}'. Expected {ObservationsEndpoint}.");
        }

        var baseUrl = configuration[BaseUrlConfigurationKey];
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            baseUrl = DefaultBaseUrl;
        }

        var baseUri = new Uri(baseUrl.TrimEnd('/') + "/", UriKind.Absolute);
        var endpoint = externalSeries.Endpoint.TrimStart('/');
        var requestUri = new Uri(baseUri, endpoint).ToString();

        var query = new Dictionary<string, string?>
        {
            ["series_id"] = externalSeries.ExternalSeriesId,
            ["api_key"] = apiKey,
            ["file_type"] = "json",
            ["units"] = externalSeries.Transform,
            ["sort_order"] = "asc"
        };

        if (fromDate.HasValue)
        {
            query["observation_start"] = fromDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        if (toDate.HasValue)
        {
            query["observation_end"] = toDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        return new Uri(QueryHelpers.AddQueryString(requestUri, query));
    }

    private bool TryParseObservation(
        FredObservation observation,
        string externalSeriesId,
        out ImportObservationDto importObservation)
    {
        importObservation = null!;

        if (!DateOnly.TryParseExact(
            observation.Date,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var observationDate))
        {
            logger.LogWarning("Skipping FRED observation for {ExternalSeriesId} because date '{ObservationDate}' could not be parsed.", externalSeriesId, observation.Date);
            return false;
        }

        if (string.IsNullOrWhiteSpace(observation.Value) || observation.Value == ".")
        {
            logger.LogWarning("Skipping FRED observation for {ExternalSeriesId} on {ObservationDate} because the value is missing.", externalSeriesId, observationDate);
            return false;
        }

        if (!decimal.TryParse(
            observation.Value,
            NumberStyles.Number | NumberStyles.AllowExponent,
            CultureInfo.InvariantCulture,
            out var value))
        {
            logger.LogWarning("Skipping FRED observation for {ExternalSeriesId} on {ObservationDate} because value '{ObservationValue}' could not be parsed.", externalSeriesId, observationDate, observation.Value);
            return false;
        }

        importObservation = new ImportObservationDto
        {
            ObservationDate = observationDate,
            Value = value,
            Source = Source,
            ExternalSeriesId = externalSeriesId,
            SourceReleaseDate = null,
            VintageDate = null,
            Notes = "Fetched from FRED."
        };
        return true;
    }

    private static string CreateSafeResponseSummary(string responseBody, string apiKey)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return "<empty>";
        }

        var sanitized = responseBody.Replace(apiKey, "<redacted>", StringComparison.Ordinal);
        sanitized = WebUtility.HtmlDecode(sanitized).Replace(apiKey, "<redacted>", StringComparison.Ordinal);

        const int maximumLength = 500;
        return sanitized.Length <= maximumLength
            ? sanitized
            : sanitized[..maximumLength] + "...";
    }

    private sealed class FredObservationsResponse
    {
        public List<FredObservation>? Observations { get; set; }
    }

    private sealed class FredObservation
    {
        public string? Date { get; set; }
        public string? Value { get; set; }
    }
}
