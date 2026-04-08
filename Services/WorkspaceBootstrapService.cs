using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
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
    private readonly string appBaseAddress;
    private readonly WorkspaceState workspaceState;
    private readonly WorkspaceSnapshotApiService workspaceSnapshotApiService;
    private readonly ILogger<WorkspaceBootstrapService>? logger;
    private readonly NavigationManager? navigationManager;

    public WorkspaceBootstrapService(HttpClient httpClient, string appBaseAddress, WorkspaceState workspaceState, WorkspaceSnapshotApiService workspaceSnapshotApiService, NavigationManager? navigationManager = null, ILogger<WorkspaceBootstrapService>? logger = null)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        this.appBaseAddress = string.IsNullOrWhiteSpace(appBaseAddress) ? throw new ArgumentNullException(nameof(appBaseAddress)) : appBaseAddress;
        this.workspaceState = workspaceState ?? throw new ArgumentNullException(nameof(workspaceState));
        this.workspaceSnapshotApiService = workspaceSnapshotApiService ?? throw new ArgumentNullException(nameof(workspaceSnapshotApiService));
        this.navigationManager = navigationManager;
        this.logger = logger;
    }

    public async Task LoadAsync(string? enterprise = null, int? fiscalYear = null, CancellationToken cancellationToken = default)
    {
        Console.WriteLine("[startup] WorkspaceBootstrapService.LoadAsync entered.");
        logger?.LogInformation("Workspace bootstrap started (enterprise={Enterprise}, fiscalYear={FiscalYear})", enterprise ?? "default", fiscalYear?.ToString() ?? "default");

        WorkspaceBootstrapData? bootstrapData = null;
        var startupSource = WorkspaceStartupSource.ApiSnapshot;
        var startupStatus = "Workspace started from the live workspace API snapshot.";

        try
        {
            bootstrapData = await workspaceSnapshotApiService.GetWorkspaceSnapshotAsync(enterprise, fiscalYear, cancellationToken);
            logger?.LogInformation("Workspace bootstrap loaded from live API snapshot for {Enterprise} FY {FiscalYear}", bootstrapData.SelectedEnterprise, bootstrapData.SelectedFiscalYear);
        }
        catch (Exception ex)
        {
            startupSource = WorkspaceStartupSource.LocalBootstrapFallback;
            startupStatus = "Workspace started from local fallback data because the workspace API was unavailable.";
            logger?.LogWarning(ex, "Workspace bootstrap fell back to local data because the live API snapshot was unavailable or invalid.");
            bootstrapData = null;
        }

        if (bootstrapData == null)
        {
            try
            {
                var bootstrapUri = navigationManager is null
                    ? new Uri(new Uri(appBaseAddress, UriKind.Absolute), "data/workspace-bootstrap.json")
                    : navigationManager.ToAbsoluteUri("data/workspace-bootstrap.json");

                bootstrapData = await httpClient.GetFromJsonAsync<WorkspaceBootstrapData>(bootstrapUri, JsonOptions, cancellationToken);
                if (bootstrapData != null)
                {
                    logger?.LogInformation("Workspace bootstrap loaded from local bootstrap file.");
                }
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Workspace bootstrap local file could not be loaded; falling back to the in-memory default workspace state.");
                bootstrapData = null;
            }
        }

        bootstrapData ??= WorkspaceDefaults.CreateBootstrapData(DateTime.UtcNow.ToString("O"));
        if (bootstrapData == null)
        {
            logger?.LogError("Workspace bootstrap failed: no API snapshot, no local bootstrap file, and no default workspace state were available.");
            throw new InvalidOperationException("Workspace bootstrap data could not be loaded.");
        }

        workspaceState.ApplyBootstrap(bootstrapData);
        workspaceState.SetStartupSource(startupSource, startupStatus);
        workspaceState.SetCurrentStateSource(startupSource, startupStatus);
        Console.WriteLine("[startup] WorkspaceBootstrapService.LoadAsync completed.");
        logger?.LogInformation("Workspace bootstrap completed from {Source} with status: {Status}", startupSource, startupStatus);
    }
}
