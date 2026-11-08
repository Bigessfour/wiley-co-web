using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using WileyCoWeb.Contracts;

namespace WileyCoWeb.Services;

/// <summary>Client for shared-contracts <c>/api/reference-data/*</c> (Prompt 3 UI + Prompt 4 ingest).</summary>
public sealed class SharedContractReferenceDataApiService(
    HttpClient httpClient,
    ILogger<SharedContractReferenceDataApiService>? logger = null)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    public async Task<IReadOnlyList<ReferenceDataActualsDocument>> GetActualsAsync(
        CancellationToken cancellationToken = default)
    {
        logger?.LogInformation("Requesting shared-contract actuals from api/reference-data/actuals");

        try
        {
            var items = await httpClient.GetFromJsonAsync<List<ReferenceDataActualsDocument>>(
                "api/reference-data/actuals",
                JsonOptions,
                cancellationToken).ConfigureAwait(false);
            return items ?? [];
        }
        catch (JsonException ex)
        {
            logger?.LogWarning(ex, "Reference-data actuals response was not valid JSON.");
            throw new InvalidOperationException("The reference-data actuals response was not valid JSON.", ex);
        }
    }

    public async Task<IReadOnlyList<ReferenceDataEventsDocument>> GetEventsAsync(
        CancellationToken cancellationToken = default)
    {
        logger?.LogInformation("Requesting shared-contract events from api/reference-data/events");

        try
        {
            var items = await httpClient.GetFromJsonAsync<List<ReferenceDataEventsDocument>>(
                "api/reference-data/events",
                JsonOptions,
                cancellationToken).ConfigureAwait(false);
            return items ?? [];
        }
        catch (JsonException ex)
        {
            logger?.LogWarning(ex, "Reference-data events response was not valid JSON.");
            throw new InvalidOperationException("The reference-data events response was not valid JSON.", ex);
        }
    }

    public async Task<ReferenceDataIngestAcceptedResponse> PostActualsAsync(
        ActualsSummaryContract actuals,
        CancellationToken cancellationToken = default)
    {
        logger?.LogInformation("POST api/reference-data/actuals enterprise={EnterpriseId} period={Period}", actuals.EnterpriseId, actuals.Period);

        using var response = await httpClient.PostAsJsonAsync(
            "api/reference-data/actuals",
            actuals,
            JsonOptions,
            cancellationToken).ConfigureAwait(false);

        if (response.StatusCode != System.Net.HttpStatusCode.Accepted)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException($"Actuals ingest failed ({(int)response.StatusCode}): {body}");
        }

        var accepted = await response.Content.ReadFromJsonAsync<ReferenceDataIngestAcceptedResponse>(JsonOptions, cancellationToken)
            .ConfigureAwait(false);
        return accepted ?? throw new InvalidOperationException("Actuals ingest returned 202 without a body.");
    }

    public async Task<ReferenceDataIngestAcceptedResponse> PostEventsAsync(
        IReadOnlyList<OperationalEventContract> events,
        CancellationToken cancellationToken = default)
    {
        logger?.LogInformation("POST api/reference-data/events count={Count}", events.Count);

        using var response = await httpClient.PostAsJsonAsync(
            "api/reference-data/events",
            events,
            JsonOptions,
            cancellationToken).ConfigureAwait(false);

        if (response.StatusCode != System.Net.HttpStatusCode.Accepted)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException($"Events ingest failed ({(int)response.StatusCode}): {body}");
        }

        var accepted = await response.Content.ReadFromJsonAsync<ReferenceDataIngestAcceptedResponse>(JsonOptions, cancellationToken)
            .ConfigureAwait(false);
        return accepted ?? throw new InvalidOperationException("Events ingest returned 202 without a body.");
    }
}
