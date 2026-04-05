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

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        WorkspaceBootstrapData? bootstrapData = null;

        try
        {
            bootstrapData = await httpClient.GetFromJsonAsync<WorkspaceBootstrapData>("api/workspace/snapshot", JsonOptions, cancellationToken);
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
}