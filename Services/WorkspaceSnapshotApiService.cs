using System.Net.Http.Json;
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
        WorkspaceBootstrapData? snapshot;

        try
        {
            snapshot = await httpClient.GetFromJsonAsync<WorkspaceBootstrapData>(requestUri, JsonOptions, cancellationToken);
        }
        catch (JsonException ex)
        {
            logger?.LogWarning(ex, "Workspace snapshot response from {RequestUri} was not valid JSON.", requestUri);
            throw new InvalidOperationException($"The workspace snapshot response from {requestUri} was not valid JSON.", ex);
        }

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

        using var content = JsonContent.Create(snapshot, options: JsonOptions);
        logger?.LogInformation("Saving rate snapshot payload of type {SnapshotType}", snapshot.GetType().Name);
        var response = await httpClient.PostAsync("api/workspace/snapshot", content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            logger?.LogWarning("Rate snapshot save failed with status {StatusCode}", (int)response.StatusCode);
            throw new InvalidOperationException($"Saving the workspace snapshot failed with status {(int)response.StatusCode}: {responseBody}");
        }

        var savedSnapshot = await response.Content.ReadFromJsonAsync<WorkspaceSnapshotSaveResponse>(JsonOptions, cancellationToken);
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
        WorkspaceScenarioCollectionResponse? response;

        try
        {
            response = await httpClient.GetFromJsonAsync<WorkspaceScenarioCollectionResponse>(requestUri, JsonOptions, cancellationToken);
        }
        catch (JsonException ex)
        {
            logger?.LogWarning(ex, "Workspace scenario response from {RequestUri} was not valid JSON.", requestUri);
            throw new InvalidOperationException($"The workspace scenarios response from {requestUri} was not valid JSON.", ex);
        }

        var scenarioResponse = response ?? new WorkspaceScenarioCollectionResponse([]);
        logger?.LogInformation("Workspace scenarios loaded: {Count}", scenarioResponse.Scenarios.Count);
        return scenarioResponse;
    }

    public async Task<WorkspaceScenarioSummaryResponse> SaveScenarioAsync(WorkspaceScenarioSaveRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var content = JsonContent.Create(request, options: JsonOptions);
        logger?.LogInformation("Saving workspace scenario {ScenarioName} for {Enterprise} FY {FiscalYear}", request.ScenarioName, request.Snapshot.SelectedEnterprise, request.Snapshot.SelectedFiscalYear);
        var response = await httpClient.PostAsync("api/workspace/scenarios", content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            logger?.LogWarning("Workspace scenario save failed with status {StatusCode}", (int)response.StatusCode);
            throw new InvalidOperationException($"Saving the workspace scenario failed with status {(int)response.StatusCode}: {responseBody}");
        }

        var savedScenario = await response.Content.ReadFromJsonAsync<WorkspaceScenarioSummaryResponse>(JsonOptions, cancellationToken);
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

        using var content = JsonContent.Create(request, options: JsonOptions);
        logger?.LogInformation("Saving workspace baseline for {Enterprise} FY {FiscalYear}", request.SelectedEnterprise, request.SelectedFiscalYear);
        var response = await httpClient.PutAsync("api/workspace/baseline", content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            logger?.LogWarning("Workspace baseline save failed with status {StatusCode}", (int)response.StatusCode);
            throw new InvalidOperationException($"Saving the workspace baseline failed with status {(int)response.StatusCode}: {responseBody}");
        }

        var savedBaseline = await response.Content.ReadFromJsonAsync<WorkspaceBaselineUpdateResponse>(JsonOptions, cancellationToken);
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
        WorkspaceBootstrapData? snapshot;

        try
        {
            snapshot = await httpClient.GetFromJsonAsync<WorkspaceBootstrapData>($"api/workspace/scenarios/{snapshotId}", JsonOptions, cancellationToken);
        }
        catch (JsonException ex)
        {
            logger?.LogWarning(ex, "Workspace scenario snapshot {SnapshotId} response was not valid JSON.", snapshotId);
            throw new InvalidOperationException($"The saved scenario payload for {snapshotId} was not valid JSON.", ex);
        }

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
}