using System.Text.Json;
using Microsoft.Extensions.Logging;
using WileyCoWeb.Contracts;
using WileyCoWeb.State;

namespace WileyCoWeb.Services;

public sealed class WorkspaceBootstrapService(WorkspaceState workspaceState, WorkspaceSnapshotApiService workspaceSnapshotApiService, ILogger<WorkspaceBootstrapService>? logger = null)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public async Task LoadAsync(string? enterprise = null, int? fiscalYear = null, CancellationToken cancellationToken = default)
    {
        Console.WriteLine("[startup] WorkspaceBootstrapService.LoadAsync entered.");
        logger?.LogInformation("Workspace bootstrap started (enterprise={Enterprise}, fiscalYear={FiscalYear})", enterprise ?? "default", fiscalYear?.ToString() ?? "default");

        const WorkspaceStartupSource startupSource = WorkspaceStartupSource.ApiSnapshot;
        const string startupStatus = "Workspace started from the live workspace API snapshot.";

        WorkspaceBootstrapData bootstrapData;

        try
        {
            bootstrapData = await workspaceSnapshotApiService.GetWorkspaceSnapshotAsync(enterprise, fiscalYear, cancellationToken);
            logger?.LogInformation("Workspace bootstrap loaded from live API snapshot for {Enterprise} FY {FiscalYear}", bootstrapData.SelectedEnterprise, bootstrapData.SelectedFiscalYear);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Workspace bootstrap failed because the live API snapshot was unavailable or invalid.");
            throw new InvalidOperationException("Workspace bootstrap requires a live API snapshot from production data.", ex);
        }

        workspaceState.ApplyBootstrap(bootstrapData);
        workspaceState.SetStartupSource(startupSource, startupStatus);
        workspaceState.SetCurrentStateSource(startupSource, startupStatus);
        Console.WriteLine("[startup] WorkspaceBootstrapService.LoadAsync completed.");
        logger?.LogInformation("Workspace bootstrap completed from {Source} with status: {Status}", startupSource, startupStatus);
    }
}
