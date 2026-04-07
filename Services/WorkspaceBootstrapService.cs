using System.Text.Json;
using System.Net.Http.Json;
using WileyCoWeb.Contracts;
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
    private readonly WorkspaceSnapshotApiService workspaceSnapshotApiService;

    public WorkspaceBootstrapService(HttpClient httpClient, WorkspaceState workspaceState, WorkspaceSnapshotApiService workspaceSnapshotApiService)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        this.workspaceState = workspaceState ?? throw new ArgumentNullException(nameof(workspaceState));
        this.workspaceSnapshotApiService = workspaceSnapshotApiService ?? throw new ArgumentNullException(nameof(workspaceSnapshotApiService));
    }

    public async Task LoadAsync(string? enterprise = null, int? fiscalYear = null, CancellationToken cancellationToken = default)
    {
        WorkspaceBootstrapData? bootstrapData = null;
        var startupSource = WorkspaceStartupSource.ApiSnapshot;
        var startupStatus = "Workspace started from the live workspace API snapshot.";

        try
        {
            bootstrapData = await workspaceSnapshotApiService.GetWorkspaceSnapshotAsync(enterprise, fiscalYear, cancellationToken);
        }
        catch (HttpRequestException)
        {
            startupSource = WorkspaceStartupSource.LocalBootstrapFallback;
            startupStatus = "Workspace started from local fallback data because the workspace API was unavailable.";
            bootstrapData = null;
        }
        catch (JsonException)
        {
            startupSource = WorkspaceStartupSource.LocalBootstrapFallback;
            startupStatus = "Workspace started from local fallback data because the workspace API response could not be parsed.";
            bootstrapData = null;
        }

        bootstrapData ??= await httpClient.GetFromJsonAsync<WorkspaceBootstrapData>("data/workspace-bootstrap.json", JsonOptions, cancellationToken);
        if (bootstrapData == null)
        {
            throw new InvalidOperationException("Workspace bootstrap data could not be loaded.");
        }

        workspaceState.ApplyBootstrap(bootstrapData);
        workspaceState.SetStartupSource(startupSource, startupStatus);
        workspaceState.SetCurrentStateSource(startupSource, startupStatus);
    }
}
