using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using WileyCoWeb.Contracts;

namespace WileyCoWeb.Services;

public sealed class CapitalGapApiService(HttpClient httpClient, ILogger<CapitalGapApiService>? logger = null)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    public async Task<CapitalGapResponse> GetAsync(CapitalGapRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        logger?.LogInformation(
            "Requesting capital gap for {Enterprise} FY {FiscalYear}",
            request.SelectedEnterprise,
            request.SelectedFiscalYear);

        var payload = await httpClient.SendJsonAsync<CapitalGapResponse>(
            HttpMethod.Post,
            "api/workspace/capital-gap",
            request,
            JsonOptions,
            "The capital gap response was not valid JSON.",
            (statusCode, responseBody) => new InvalidOperationException(BuildFailureMessage(statusCode, responseBody)),
            cancellationToken).ConfigureAwait(false);

        return payload ?? throw new InvalidOperationException("The capital gap response was empty.");
    }

    private static string BuildFailureMessage(HttpStatusCode statusCode, string? responseBody)
    {
        var detail = HttpProblemDetailsParser.ExtractMessage(responseBody);
        return string.IsNullOrWhiteSpace(detail)
            ? $"Loading capital gap failed with status {(int)statusCode}."
            : $"Loading capital gap failed with status {(int)statusCode}: {detail}";
    }
}