using Microsoft.Extensions.Logging;
using WileyCoWeb.Contracts;
using WileyCoWeb.State;

namespace WileyCoWeb.Services;

public sealed class WorkspaceBootstrapService(
    WorkspaceState workspaceState,
    WorkspaceSnapshotApiService workspaceSnapshotApiService,
    WorkspaceLocalBootstrapService workspaceLocalBootstrapService,
    ILogger<WorkspaceBootstrapService>? logger = null)
{
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
            startupSource = WorkspaceStartupSource.LocalBootstrapFallback;

            try
            {
                bootstrapData = await workspaceLocalBootstrapService.LoadAsync(cancellationToken).ConfigureAwait(false)
                    ?? throw new InvalidOperationException("Workspace local bootstrap asset returned no payload.");
                startupStatus = "Workspace started from local fallback data because the workspace API was unavailable. A local workspace bootstrap asset is active until the API reconnects.";
            }
            catch (Exception localFallbackEx)
            {
                logger?.LogWarning(localFallbackEx, "Workspace local bootstrap asset fallback was unavailable or invalid. Using generated fallback state.");
                bootstrapData = CreateLocalFallbackBootstrap(enterprise, fiscalYear);
                startupStatus = "Workspace started from local fallback data because the workspace API was unavailable. Saved scenarios are temporarily unavailable.";
            }
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
