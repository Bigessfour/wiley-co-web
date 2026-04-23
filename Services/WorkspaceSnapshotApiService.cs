using System.Net;
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
        var snapshot = await httpClient.GetJsonAsync<WorkspaceBootstrapData>(
            requestUri,
            JsonOptions,
            $"The workspace snapshot response from {requestUri} was not valid JSON.",
            (statusCode, responseBody) => new InvalidOperationException(BuildFailureMessage("workspace snapshot request", statusCode, responseBody)),
            cancellationToken).ConfigureAwait(false);

        if (snapshot == null)
        {
            logger?.LogWarning("Workspace snapshot request returned no payload from {RequestUri}", requestUri);
            throw new InvalidOperationException("The workspace snapshot response was empty.");
        }

        logger?.LogInformation("Workspace snapshot loaded for {Enterprise} FY {FiscalYear}", snapshot.SelectedEnterprise, snapshot.SelectedFiscalYear);
        return snapshot;
    }

    public async Task<WorkspaceSnapshotSaveResponse> SaveRateSnapshotAsync(object snapshot, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        logger?.LogInformation("Saving rate snapshot payload of type {SnapshotType}", snapshot.GetType().Name);
        var savedSnapshot = await httpClient.SendJsonAsync<WorkspaceSnapshotSaveResponse>(
            HttpMethod.Post,
            "api/workspace/snapshot",
            snapshot,
            JsonOptions,
            "The workspace snapshot save response was not valid JSON.",
            (statusCode, responseBody) => new InvalidOperationException(BuildFailureMessage("rate snapshot save", statusCode, responseBody)),
            cancellationToken).ConfigureAwait(false);

        if (savedSnapshot == null)
        {
            logger?.LogWarning("Rate snapshot save returned an empty payload.");
            throw new InvalidOperationException("The workspace snapshot save response was empty.");
        }

        logger?.LogInformation("Rate snapshot saved with ID {SnapshotId}", savedSnapshot.SnapshotId);
        return savedSnapshot;
    }

    public async Task<WorkspaceScenarioCollectionResponse> GetScenariosAsync(string? enterprise = null, int? fiscalYear = null, CancellationToken cancellationToken = default)
    {
        var requestUri = BuildScenarioRequestUri(enterprise, fiscalYear);
        logger?.LogInformation("Requesting workspace scenarios from {RequestUri}", requestUri);
        var scenarioResponse = await httpClient.GetJsonAsync<WorkspaceScenarioCollectionResponse>(
            requestUri,
            JsonOptions,
            $"The workspace scenarios response from {requestUri} was not valid JSON.",
            (statusCode, responseBody) => new InvalidOperationException(BuildFailureMessage("workspace scenarios request", statusCode, responseBody)),
            cancellationToken).ConfigureAwait(false) ?? new WorkspaceScenarioCollectionResponse([]);

        logger?.LogInformation("Workspace scenarios loaded: {Count}", scenarioResponse.Scenarios.Count);
        return scenarioResponse;
    }

    public async Task<WorkspaceScenarioSummaryResponse> SaveScenarioAsync(WorkspaceScenarioSaveRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        logger?.LogInformation("Saving workspace scenario {ScenarioName} for {Enterprise} FY {FiscalYear}", request.ScenarioName, request.Snapshot.SelectedEnterprise, request.Snapshot.SelectedFiscalYear);
        var savedScenario = await httpClient.SendJsonAsync<WorkspaceScenarioSummaryResponse>(
            HttpMethod.Post,
            "api/workspace/scenarios",
            request,
            JsonOptions,
            "The workspace scenario save response was not valid JSON.",
            (statusCode, responseBody) => new InvalidOperationException(BuildFailureMessage("workspace scenario save", statusCode, responseBody)),
            cancellationToken).ConfigureAwait(false);

        if (savedScenario == null)
        {
            logger?.LogWarning("Workspace scenario save returned an empty payload.");
            throw new InvalidOperationException("The workspace scenario save response was empty.");
        }

        logger?.LogInformation("Workspace scenario saved with snapshot ID {SnapshotId}", savedScenario.SnapshotId);
        return savedScenario;
    }

    public async Task<WorkspaceBaselineUpdateResponse> SaveWorkspaceBaselineAsync(WorkspaceBaselineUpdateRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        logger?.LogInformation("Saving workspace baseline for {Enterprise} FY {FiscalYear}", request.SelectedEnterprise, request.SelectedFiscalYear);
        var savedBaseline = await httpClient.SendJsonAsync<WorkspaceBaselineUpdateResponse>(
            HttpMethod.Put,
            "api/workspace/baseline",
            request,
            JsonOptions,
            "The workspace baseline save response was not valid JSON.",
            (statusCode, responseBody) => new InvalidOperationException(BuildFailureMessage("workspace baseline save", statusCode, responseBody)),
            cancellationToken).ConfigureAwait(false);

        if (savedBaseline == null)
        {
            logger?.LogWarning("Workspace baseline save returned an empty payload.");
            throw new InvalidOperationException("The workspace baseline save response was empty.");
        }

        logger?.LogInformation("Workspace baseline saved for {Enterprise} FY {FiscalYear}", savedBaseline.SelectedEnterprise, savedBaseline.SelectedFiscalYear);
        return savedBaseline;
    }

    public async Task<WorkspaceBootstrapData> GetScenarioSnapshotAsync(long snapshotId, CancellationToken cancellationToken = default)
    {
        var operationName = $"saved scenario payload for {snapshotId}";
        var snapshot = await httpClient.GetJsonAsync<WorkspaceBootstrapData>(
            $"api/workspace/scenarios/{snapshotId}",
            JsonOptions,
            $"The saved scenario payload for {snapshotId} was not valid JSON.",
            (statusCode, responseBody) => new InvalidOperationException(BuildFailureMessage(operationName, statusCode, responseBody)),
            cancellationToken).ConfigureAwait(false);

        if (snapshot == null)
        {
            logger?.LogWarning("Workspace scenario snapshot {SnapshotId} returned no payload", snapshotId);
            throw new InvalidOperationException("The saved scenario payload was empty.");
        }

        logger?.LogInformation("Loaded workspace scenario snapshot {SnapshotId} for {Enterprise} FY {FiscalYear}", snapshotId, snapshot.SelectedEnterprise, snapshot.SelectedFiscalYear);
        return snapshot;
    }

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

    private static string BuildQueryString(string? enterprise, int? fiscalYear)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(enterprise))
        {
            parts.Add($"enterprise={Uri.EscapeDataString(enterprise.Trim())}");
        }

        if (fiscalYear is > 0)
        {
            parts.Add($"fiscalYear={fiscalYear.Value}");
        }

        return parts.Count == 0 ? string.Empty : $"?{string.Join("&", parts)}";
    }

    private static string BuildFailureMessage(string operationName, HttpStatusCode statusCode, string? responseBody)
    {
        var detail = HttpProblemDetailsParser.ExtractMessage(responseBody);
        return string.IsNullOrWhiteSpace(detail)
            ? $"{operationName} failed with status {(int)statusCode}."
            : $"{operationName} failed with status {(int)statusCode}: {detail}";
    }
}