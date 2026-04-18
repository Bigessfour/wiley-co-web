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

        var startupSource = WorkspaceStartupSource.ApiSnapshot;
        var startupStatus = "Workspace started from the live workspace API snapshot.";

        WorkspaceBootstrapData bootstrapData;

        try
        {
            bootstrapData = await workspaceSnapshotApiService.GetWorkspaceSnapshotAsync(enterprise, fiscalYear, cancellationToken);
            logger?.LogInformation("Workspace bootstrap loaded from live API snapshot for {Enterprise} FY {FiscalYear}", bootstrapData.SelectedEnterprise, bootstrapData.SelectedFiscalYear);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Workspace bootstrap is falling back because the live API snapshot was unavailable or invalid.");
            bootstrapData = CreateLocalFallbackBootstrap(enterprise, fiscalYear);
            startupSource = WorkspaceStartupSource.LocalBootstrapFallback;
            startupStatus = "Workspace started from local fallback data because the workspace API was unavailable. Saved scenarios are temporarily unavailable.";
        }

        workspaceState.ApplyBootstrap(bootstrapData);
        workspaceState.SetStartupSource(startupSource, startupStatus);
        workspaceState.SetCurrentStateSource(startupSource, startupStatus);
        Console.WriteLine("[startup] WorkspaceBootstrapService.LoadAsync completed.");
        logger?.LogInformation("Workspace bootstrap completed from {Source} with status: {Status}", startupSource, startupStatus);
    }

    private WorkspaceBootstrapData CreateLocalFallbackBootstrap(string? enterprise, int? fiscalYear)
    {
        var currentState = workspaceState.ToBootstrapData();

        return new WorkspaceBootstrapData(
            string.IsNullOrWhiteSpace(enterprise) ? currentState.SelectedEnterprise : enterprise.Trim(),
            fiscalYear is > 0 ? fiscalYear.Value : currentState.SelectedFiscalYear,
            string.Empty,
            currentState.CurrentRate,
            currentState.TotalCosts,
            currentState.ProjectedVolume,
            DateTime.UtcNow.ToString("O"))
        {
            EnterpriseOptions = currentState.EnterpriseOptions ?? [],
            FiscalYearOptions = currentState.FiscalYearOptions ?? [],
            CustomerServiceOptions = currentState.CustomerServiceOptions ?? [],
            CustomerCityLimitOptions = currentState.CustomerCityLimitOptions ?? [],
            CustomerRows = currentState.CustomerRows ?? [],
            ProjectionRows = currentState.ProjectionRows ?? [],
            ScenarioItems = []
        };
    }
}
