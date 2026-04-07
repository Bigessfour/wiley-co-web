using System.Net.Http.Json;
using System.Text.Json;
using WileyCoWeb.Contracts;
using WileyCoWeb.State;

namespace WileyCoWeb.Services;

public sealed class WorkspaceSnapshotApiService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    private readonly HttpClient httpClient;

    public WorkspaceSnapshotApiService(HttpClient httpClient)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public async Task<WorkspaceBootstrapData> GetWorkspaceSnapshotAsync(string? enterprise = null, int? fiscalYear = null, CancellationToken cancellationToken = default)
    {
        var requestUri = BuildSnapshotRequestUri(enterprise, fiscalYear);
        var snapshot = await httpClient.GetFromJsonAsync<WorkspaceBootstrapData>(requestUri, JsonOptions, cancellationToken);
        return snapshot ?? throw new InvalidOperationException("The workspace snapshot response was empty.");
    }

    public async Task<WorkspaceSnapshotSaveResponse> SaveRateSnapshotAsync(object snapshot, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        using var content = JsonContent.Create(snapshot, options: JsonOptions);
        var response = await httpClient.PostAsync("api/workspace/snapshot", content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Saving the workspace snapshot failed with status {(int)response.StatusCode}: {responseBody}");
        }

        var savedSnapshot = await response.Content.ReadFromJsonAsync<WorkspaceSnapshotSaveResponse>(JsonOptions, cancellationToken);
        return savedSnapshot ?? throw new InvalidOperationException("The workspace snapshot save response was empty.");
    }

    public async Task<WorkspaceScenarioCollectionResponse> GetScenariosAsync(string? enterprise = null, int? fiscalYear = null, CancellationToken cancellationToken = default)
    {
        var requestUri = BuildScenarioRequestUri(enterprise, fiscalYear);
        var response = await httpClient.GetFromJsonAsync<WorkspaceScenarioCollectionResponse>(requestUri, JsonOptions, cancellationToken);
        return response ?? new WorkspaceScenarioCollectionResponse([]);
    }

    public async Task<WorkspaceScenarioSummaryResponse> SaveScenarioAsync(WorkspaceScenarioSaveRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var content = JsonContent.Create(request, options: JsonOptions);
        var response = await httpClient.PostAsync("api/workspace/scenarios", content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Saving the workspace scenario failed with status {(int)response.StatusCode}: {responseBody}");
        }

        var savedScenario = await response.Content.ReadFromJsonAsync<WorkspaceScenarioSummaryResponse>(JsonOptions, cancellationToken);
        return savedScenario ?? throw new InvalidOperationException("The workspace scenario save response was empty.");
    }

    public async Task<WorkspaceBaselineUpdateResponse> SaveWorkspaceBaselineAsync(WorkspaceBaselineUpdateRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var content = JsonContent.Create(request, options: JsonOptions);
        var response = await httpClient.PutAsync("api/workspace/baseline", content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Saving the workspace baseline failed with status {(int)response.StatusCode}: {responseBody}");
        }

        var savedBaseline = await response.Content.ReadFromJsonAsync<WorkspaceBaselineUpdateResponse>(JsonOptions, cancellationToken);
        return savedBaseline ?? throw new InvalidOperationException("The workspace baseline save response was empty.");
    }

    public async Task<WorkspaceBootstrapData> GetScenarioSnapshotAsync(long snapshotId, CancellationToken cancellationToken = default)
    {
        var snapshot = await httpClient.GetFromJsonAsync<WorkspaceBootstrapData>($"api/workspace/scenarios/{snapshotId}", JsonOptions, cancellationToken);
        return snapshot ?? throw new InvalidOperationException("The saved scenario payload was empty.");
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