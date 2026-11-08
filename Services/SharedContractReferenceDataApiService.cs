using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using WileyCoWeb.Contracts;

namespace WileyCoWeb.Services;

/// <summary>Read-only client for shared-contracts reference-data ingest (Prompt 3 UI).</summary>
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
}
