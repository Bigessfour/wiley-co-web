using System.Net;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using WileyCoWeb.Contracts;
using WileyCoWeb.State;

namespace WileyCoWeb.Services;

public sealed class WorkspaceSnapshotApiService(HttpClient httpClient, ILogger<WorkspaceSnapshotApiService>? logger = null)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    public async Task<WorkspaceBootstrapData> GetWorkspaceSnapshotAsync(string? enterprise = null, int? fiscalYear = null, CancellationToken cancellationToken = default)
    {
        var requestUri = BuildSnapshotRequestUri(enterprise, fiscalYear);
        logger?.LogInformation("Requesting workspace snapshot from {RequestUri}", requestUri);
        return await GetRequiredJsonAsync<WorkspaceBootstrapData>(
            requestUri,
            $"The workspace snapshot response from {requestUri} was not valid JSON.",
            "workspace snapshot request",
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<WorkspaceSnapshotSaveResponse> SaveRateSnapshotAsync(object snapshot, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        logger?.LogInformation("Saving rate snapshot payload of type {SnapshotType}", snapshot.GetType().Name);
        return await SendRequiredJsonAsync<WorkspaceSnapshotSaveResponse>(
            HttpMethod.Post,
            "api/workspace/snapshot",
            snapshot,
            "The workspace snapshot save response was not valid JSON.",
            "rate snapshot save",
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<WorkspaceScenarioCollectionResponse> GetScenariosAsync(string? enterprise = null, int? fiscalYear = null, CancellationToken cancellationToken = default)
    {
        var requestUri = BuildScenarioRequestUri(enterprise, fiscalYear);
        logger?.LogInformation("Requesting workspace scenarios from {RequestUri}", requestUri);
        return await GetJsonOrDefaultAsync(
            requestUri,
            $"The workspace scenarios response from {requestUri} was not valid JSON.",
            new WorkspaceScenarioCollectionResponse([]),
            "workspace scenarios request",
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<WorkspaceScenarioSummaryResponse> SaveScenarioAsync(WorkspaceScenarioSaveRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        logger?.LogInformation("Saving workspace scenario {ScenarioName} for {Enterprise} FY {FiscalYear}", request.ScenarioName, request.Snapshot.SelectedEnterprise, request.Snapshot.SelectedFiscalYear);
        return await SendRequiredJsonAsync<WorkspaceScenarioSummaryResponse>(
            HttpMethod.Post,
            "api/workspace/scenarios",
            request,
            "The workspace scenario save response was not valid JSON.",
            "workspace scenario save",
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<WorkspaceBaselineUpdateResponse> SaveWorkspaceBaselineAsync(WorkspaceBaselineUpdateRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        logger?.LogInformation("Saving workspace baseline for {Enterprise} FY {FiscalYear}", request.SelectedEnterprise, request.SelectedFiscalYear);
        return await SendRequiredJsonAsync<WorkspaceBaselineUpdateResponse>(
            HttpMethod.Put,
            "api/workspace/baseline",
            request,
            "The workspace baseline save response was not valid JSON.",
            "workspace baseline save",
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<WorkspaceBootstrapData> GetScenarioSnapshotAsync(long snapshotId, CancellationToken cancellationToken = default)
    {
        var requestUri = $"api/workspace/scenarios/{snapshotId}";
        return await GetRequiredJsonAsync<WorkspaceBootstrapData>(
            requestUri,
            $"The saved scenario payload for {snapshotId} was not valid JSON.",
            $"saved scenario payload for {snapshotId}",
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<TResponse> GetRequiredJsonAsync<TResponse>(string requestUri, string invalidJsonMessage, string operationName, CancellationToken cancellationToken)
    {
        var payload = await httpClient.GetJsonAsync<TResponse>(
            requestUri,
            JsonOptions,
            invalidJsonMessage,
            BuildFailureException(operationName),
            cancellationToken).ConfigureAwait(false);

        return payload ?? throw new InvalidOperationException($"{operationName} response was empty.");
    }

    private async Task<TResponse> GetJsonOrDefaultAsync<TResponse>(string requestUri, string invalidJsonMessage, TResponse defaultValue, string operationName, CancellationToken cancellationToken)
    {
        var payload = await httpClient.GetJsonAsync<TResponse>(
            requestUri,
            JsonOptions,
            invalidJsonMessage,
            BuildFailureException(operationName),
            cancellationToken).ConfigureAwait(false);

        return payload ?? defaultValue;
    }

    private async Task<TResponse> SendRequiredJsonAsync<TResponse>(HttpMethod method, string requestUri, object requestBody, string invalidJsonMessage, string operationName, CancellationToken cancellationToken)
    {
        var payload = await httpClient.SendJsonAsync<TResponse>(
            method,
            requestUri,
            requestBody,
            JsonOptions,
            invalidJsonMessage,
            BuildFailureException(operationName),
            cancellationToken).ConfigureAwait(false);

        return payload ?? throw new InvalidOperationException($"{operationName} response was empty.");
    }

    private static Func<HttpStatusCode, string?, Exception> BuildFailureException(string operationName)
        => (statusCode, responseBody) => new InvalidOperationException(BuildFailureMessage(operationName, statusCode, responseBody));

    private static string BuildFailureMessage(string operationName, HttpStatusCode statusCode, string? responseBody)
    {
        var detail = HttpProblemDetailsParser.ExtractMessage(responseBody);
        return string.IsNullOrWhiteSpace(detail)
            ? $"{operationName} failed with status {(int)statusCode}."
            : $"{operationName} failed with status {(int)statusCode}: {detail}";
    }

    private static string BuildQueryString(string? enterprise, int? fiscalYear)
    {
        var parts = new[]
        {
            BuildEnterpriseQueryPart(enterprise),
            BuildFiscalYearQueryPart(fiscalYear)
        }.Where(part => part is not null).Select(part => part!);

        var query = string.Join("&", parts);
        return string.IsNullOrEmpty(query) ? string.Empty : $"?{query}";
    }

    private static string? BuildEnterpriseQueryPart(string? enterprise)
        => string.IsNullOrWhiteSpace(enterprise)
            ? null
            : $"enterprise={Uri.EscapeDataString(enterprise.Trim())}";

    private static string? BuildFiscalYearQueryPart(int? fiscalYear)
        => fiscalYear is > 0
            ? $"fiscalYear={fiscalYear.Value}"
            : null;

    private static string BuildSnapshotRequestUri(string? enterprise, int? fiscalYear)
    {
        var query = BuildQueryString(enterprise, fiscalYear);
        return string.IsNullOrEmpty(query) ? "api/workspace/snapshot" : $"api/workspace/snapshot{query}";
    }

    private static string BuildScenarioRequestUri(string? enterprise, int? fiscalYear)
    {
        var query = BuildQueryString(enterprise, fiscalYear);
        return string.IsNullOrEmpty(query) ? "api/workspace/scenarios" : $"api/workspace/scenarios{query}";
    }
}
