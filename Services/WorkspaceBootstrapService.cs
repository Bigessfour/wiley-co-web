using System.Text.Json;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using WileyCoWeb.State;

namespace WileyCoWeb.Services;

public sealed class WorkspaceBootstrapService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private readonly HttpClient httpClient;
    private readonly WorkspaceState workspaceState;

    public WorkspaceBootstrapService(HttpClient httpClient, WorkspaceState workspaceState)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        this.workspaceState = workspaceState ?? throw new ArgumentNullException(nameof(workspaceState));
    }

    public Task LoadAsync(CancellationToken cancellationToken = default)
    {
        return LoadAsync(null, null, cancellationToken);
    }

    public async Task LoadAsync(string? enterprise, int? fiscalYear, CancellationToken cancellationToken = default)
    {
        WorkspaceBootstrapData? bootstrapData = null;

        try
        {
            bootstrapData = await httpClient.GetFromJsonAsync<WorkspaceBootstrapData>(BuildSnapshotRequestUri(enterprise, fiscalYear), JsonOptions, cancellationToken);
        }
        catch (HttpRequestException)
        {
            bootstrapData = null;
        }

        bootstrapData ??= await httpClient.GetFromJsonAsync<WorkspaceBootstrapData>("data/workspace-bootstrap.json", JsonOptions, cancellationToken);
        if (bootstrapData == null)
        {
            throw new InvalidOperationException("Workspace bootstrap data could not be loaded.");
        }

        workspaceState.ApplyBootstrap(bootstrapData);
    }

    private static string BuildSnapshotRequestUri(string? enterprise, int? fiscalYear)
    {
        var requestUri = "api/workspace/snapshot";
        var hasEnterprise = !string.IsNullOrWhiteSpace(enterprise);
        var hasFiscalYear = fiscalYear is > 0;

        if (!hasEnterprise && !hasFiscalYear)
        {
            return requestUri;
        }

        var queryParts = new List<string>(2);

        if (hasEnterprise)
        {
            queryParts.Add($"enterprise={Uri.EscapeDataString(enterprise!.Trim())}");
        }

        if (hasFiscalYear)
        {
            queryParts.Add($"fiscalYear={fiscalYear}");
        }

        return $"{requestUri}?{string.Join('&', queryParts)}";
    }
}