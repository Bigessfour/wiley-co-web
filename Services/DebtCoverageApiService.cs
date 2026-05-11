using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using WileyCoWeb.Contracts;

namespace WileyCoWeb.Services;

public sealed class DebtCoverageApiService(HttpClient httpClient, ILogger<DebtCoverageApiService>? logger = null)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    public async Task<DebtCoverageResponse> GetAsync(DebtCoverageRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        logger?.LogInformation(
            "Requesting debt coverage for {Enterprise} FY {FiscalYear}",
            request.SelectedEnterprise,
            request.SelectedFiscalYear);

        var payload = await httpClient.SendJsonAsync<DebtCoverageResponse>(
            HttpMethod.Post,
            "api/workspace/debt-coverage",
            request,
            JsonOptions,
            "The debt coverage response was not valid JSON.",
            (statusCode, responseBody) => new InvalidOperationException(BuildFailureMessage(statusCode, responseBody)),
            cancellationToken).ConfigureAwait(false);

        return payload ?? throw new InvalidOperationException("The debt coverage response was empty.");
    }

    private static string BuildFailureMessage(HttpStatusCode statusCode, string? responseBody)
    {
        var detail = HttpProblemDetailsParser.ExtractMessage(responseBody);
        return string.IsNullOrWhiteSpace(detail)
            ? $"Loading debt coverage failed with status {(int)statusCode}."
            : $"Loading debt coverage failed with status {(int)statusCode}: {detail}";
    }
}