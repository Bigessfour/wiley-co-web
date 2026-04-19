using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using WileyCoWeb.Contracts;
using WileyCoWeb.Services;
using WileyCoWeb.State;

namespace WileyCoWeb.Components.Pages;

#pragma warning disable S2325
public partial class WileyWorkspaceBase : ComponentBase, IDisposable
{
    protected enum WorkspaceApiHealth { Unknown, Healthy, Degraded }

    [Parameter]
    public string? Panel { get; set; }

    [Inject]
    protected WorkspaceState WorkspaceState { get; set; } = default!;

    [Inject]
    protected NavigationManager NavigationManager { get; set; } = default!;

    [Inject]
    protected WorkspacePersistenceService WorkspacePersistenceService { get; set; } = default!;

    [Inject]
    protected WorkspaceSnapshotApiService WorkspaceSnapshotApiService { get; set; } = default!;

    [Inject]
    protected WorkspaceDocumentExportService WorkspaceDocumentExportService { get; set; } = default!;

    [Inject]
    protected BrowserDownloadService BrowserDownloadService { get; set; } = default!;

    [Inject]
    protected ILogger<WileyWorkspaceBase> WorkspaceLogger { get; set; } = default!;

    private bool persistenceInitialized;
    private bool isRefreshingScenarioCatalog;
    private DateTimeOffset? lastWorkspaceSyncUtc;
    private string? lastScenarioCatalogSelectionKey;
    private WorkspaceApiHealth _apiHealth = WorkspaceApiHealth.Unknown;

    protected bool IsSavingSnapshot { get; set; }
    protected bool IsSavingScenario { get; set; }
    protected bool IsSavingBaseline { get; set; }
    protected bool IsApplyingScenario { get; set; }
    protected bool IsLoadingWorkspace { get; set; }
    protected bool IsExportingDocuments { get; set; }
    protected bool IsSidebarOpen { get; set; }

    protected string SnapshotSaveStatus { get; set; } = "Ready to save rate snapshot";
    protected string BaselineSaveStatus { get; set; } = "Baseline changes are local until you save them.";
    protected string ScenarioPersistenceStatus { get; set; } = "Saved scenarios load by enterprise and fiscal year.";
    protected string WorkspaceLoadStatus { get; set; } = "Workspace initialization is pending.";
    protected string DocumentExportStatus { get; set; } = "Excel and PDF exports are ready.";

    protected long? SelectedScenarioSnapshotId { get; set; }
    protected string ScenarioDescription { get; set; } = string.Empty;
    protected IReadOnlyList<WorkspaceScenarioSummaryResponse> SavedScenarios { get; set; } = [];

    protected string SelectedEnterprise
    {
        get => WorkspaceState.SelectedEnterprise;
        set => WorkspaceState.SetSelection(value, WorkspaceState.SelectedFiscalYear);
    }

    protected int SelectedFiscalYear
    {
        get => WorkspaceState.SelectedFiscalYear;
        set => WorkspaceState.SetSelection(WorkspaceState.SelectedEnterprise, value);
    }

    protected string ActiveScenarioName
    {
        get => WorkspaceState.ActiveScenarioName;
        set => WorkspaceState.SetActiveScenarioName(value);
    }

    protected decimal CurrentRate
    {
        get => WorkspaceState.CurrentRate;
        set => WorkspaceState.SetCurrentRate(value);
    }

    protected decimal TotalCosts
    {
        get => WorkspaceState.TotalCosts;
        set => WorkspaceState.SetTotalCosts(value);
    }

    protected decimal ProjectedVolume
    {
        get => WorkspaceState.ProjectedVolume;
        set => WorkspaceState.SetProjectedVolume(value);
    }

    protected decimal RecommendedRate => WorkspaceState.RecommendedRate;
    protected decimal ScenarioAdjustedRate => WorkspaceState.AdjustedRecommendedRate;
    protected decimal ScenarioAdjustedDelta => WorkspaceState.AdjustedRateDelta;
    protected decimal ScenarioCostTotal => WorkspaceState.ScenarioCostTotal;

    protected string CurrentRateDisplay => CurrentRate.ToString("C2");
    protected string BreakEvenRateDisplay => RecommendedRate.ToString("C2");
    protected string RateDeltaDisplay => WorkspaceState.RateDelta.ToString("C2");
    protected string ScenarioAdjustedRateDisplay => WorkspaceState.AdjustedRecommendedRate.ToString("C2");
    protected string ScenarioAdjustedDeltaDisplay => WorkspaceState.AdjustedRateDelta.ToString("C2");
    protected string ScenarioCostTotalDisplay => WorkspaceState.ScenarioCostTotal.ToString("C0");
    protected string TotalCostsDisplay => TotalCosts.ToString("C0");
    protected string ProjectedVolumeDisplay => ProjectedVolume.ToString("N0");
    protected double GaugeMaximum => (double)Math.Max(RecommendedRate, CurrentRate) * 1.5d;
    protected double GaugeCurrentRateValue => (double)CurrentRate;

    protected IEnumerable<string> EnterpriseOptions => WorkspaceState.EnterpriseOptions;
    protected IEnumerable<int> FiscalYearOptions => WorkspaceState.FiscalYearOptions;
    protected IReadOnlyList<string> CustomerServiceOptions => WorkspaceState.CustomerServiceOptions;
    protected IReadOnlyList<string> CustomerCityLimitOptions => WorkspaceState.CustomerCityLimitOptions;

    protected IReadOnlyList<RateComparisonPoint> RateComparison => WorkspaceState.RateComparison;
    protected IReadOnlyList<ScenarioItem> ScenarioItems => WorkspaceState.ScenarioItems;
    protected IReadOnlyList<CustomerRow> Customers => WorkspaceState.FilteredCustomers;
    protected int FilteredCustomerCount => WorkspaceState.FilteredCustomerCount;
    protected string FilteredCustomerCountDisplay => FilteredCustomerCount.ToString();
    protected IReadOnlyList<ProjectionRow> ProjectionSeries => WorkspaceState.ProjectionSeries;
    protected bool CanApplySelectedScenario => SelectedScenarioSnapshotId is > 0;
    protected string StartupSourceStatus => WorkspaceState.StartupSourceStatus;
    protected bool IsUsingStartupFallback => WorkspaceState.IsUsingStartupFallback;
    protected string CurrentStateSourceStatus => WorkspaceState.CurrentStateSourceStatus;
    protected bool IsUsingBrowserRestoredState => WorkspaceState.IsUsingBrowserRestoredState;

    protected string CustomerSearchTerm
    {
        get => WorkspaceState.CustomerSearchTerm;
        set => WorkspaceState.SetCustomerSearchTerm(value);
    }

    protected string SelectedCustomerService
    {
        get => WorkspaceState.SelectedCustomerService;
        set => WorkspaceState.SetCustomerServiceFilter(value);
    }

    protected string SelectedCustomerCityLimits
    {
        get => WorkspaceState.SelectedCustomerCityLimits;
        set => WorkspaceState.SetCustomerCityLimitsFilter(value);
    }

    protected IReadOnlyList<string> ScenarioToolbarItems { get; } = ["Add", "Edit", "Delete", "Update", "Cancel"];

    protected string ActivePanelKey => NormalizePanelKey(Panel);
    protected bool IsOverviewMode => string.Equals(ActivePanelKey, "overview", StringComparison.Ordinal);
    protected string ActivePanelLabel => PanelNavItems.FirstOrDefault(item => item.Key == ActivePanelKey)?.Label ?? "Overview";
    protected string BreadcrumbSection => IsOverviewMode ? "Workspace Overview" : "Workspace Panel";
    protected static string HostingEnvironmentName => Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
    protected static string HostingVersion => typeof(WileyWorkspaceBase).Assembly.GetName().Version?.ToString() ?? "dev";
    protected string HostingLastSyncedDisplay => lastWorkspaceSyncUtc?.ToLocalTime().ToString("g") ?? "Not synced";
    protected string HostingPersistenceStatus => IsUsingStartupFallback ? "Fallback persistence active" : "Primary persistence active";
    protected string HostingApiHealth => _apiHealth switch
    {
        WorkspaceApiHealth.Healthy => "Healthy",
        WorkspaceApiHealth.Degraded => "Degraded",
        _ => "Unknown"
    };

    protected IReadOnlyList<WorkspacePanelNavItem> PanelNavItems { get; } =
    [
        new("overview", "Overview"),
        new("break-even", "Break-Even"),
        new("rates", "Rates"),
        new("quickbooks-import", "QuickBooks Import"),
        new("scenario", "Scenario Planner"),
        new("customers", "Customer Viewer"),
        new("trends", "Trends"),
        new("decision-support", "Decision Support"),
        new("data-dashboard", "Data Dashboard")
    ];

    protected static string GetPanelRoute(string panelKey)
    {
        var normalized = NormalizePanelKey(panelKey);
        return string.Equals(normalized, "overview", StringComparison.Ordinal)
            ? "/wiley-workspace"
            : $"/wiley-workspace/{normalized}";
    }

    protected void CloseSidebar()
    {
        IsSidebarOpen = false;
    }

    protected void OpenPanel(string panelKey)
    {
        IsSidebarOpen = false;
        NavigationManager.NavigateTo(GetPanelRoute(panelKey));
    }

    protected void ToggleSidebar()
    {
        IsSidebarOpen = !IsSidebarOpen;
    }

    protected void ClearCustomerFilters() => WorkspaceState.ClearCustomerFilters();

    protected async Task HandleEnterpriseChanged(Syncfusion.Blazor.DropDowns.ChangeEventArgs<string, string> args)
    {
        await ReloadWorkspaceAsync(args.Value, SelectedFiscalYear);
    }

    protected async Task HandleFiscalYearChanged(Syncfusion.Blazor.DropDowns.ChangeEventArgs<int, int> args)
    {
        await ReloadWorkspaceAsync(SelectedEnterprise, args.Value);
    }

    protected Task RefreshWorkspaceAsync() => ReloadWorkspaceAsync(SelectedEnterprise, SelectedFiscalYear);

    protected void HandleSavedScenarioChanged(Syncfusion.Blazor.DropDowns.ChangeEventArgs<long?, WorkspaceScenarioSummaryResponse> args)
    {
        SelectedScenarioSnapshotId = args.Value;

        var selectedScenario = SavedScenarios.FirstOrDefault(item => item.SnapshotId == SelectedScenarioSnapshotId);
        if (selectedScenario != null)
        {
            ScenarioDescription = selectedScenario.Description ?? string.Empty;
            ScenarioPersistenceStatus = $"Selected saved scenario '{selectedScenario.ScenarioName}'.";
        }
    }

    protected async Task SaveScenarioAsync()
    {
        if (IsSavingScenario)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(ActiveScenarioName))
        {
            ScenarioPersistenceStatus = "Enter a scenario name before saving.";
            return;
        }

        IsSavingScenario = true;
        ScenarioPersistenceStatus = "Saving scenario to persisted workspace storage...";
        StateHasChanged();

        try
        {
            var request = new WorkspaceScenarioSaveRequest(
                ActiveScenarioName.Trim(),
                string.IsNullOrWhiteSpace(ScenarioDescription) ? null : ScenarioDescription.Trim(),
                WorkspaceState.ToBootstrapData());

            var savedScenario = await WorkspaceSnapshotApiService.SaveScenarioAsync(request);
            SelectedScenarioSnapshotId = savedScenario.SnapshotId;
            WorkspaceState.SetActiveScenarioName(savedScenario.ScenarioName);
            ScenarioDescription = savedScenario.Description ?? ScenarioDescription;
            ScenarioPersistenceStatus = $"Saved scenario '{savedScenario.ScenarioName}' at {savedScenario.CreatedAtUtc}.";
            lastWorkspaceSyncUtc = DateTimeOffset.UtcNow;
            _apiHealth = WorkspaceApiHealth.Healthy;
            await RefreshScenarioCatalogAsync(savedScenario.SnapshotId);
        }
        catch (Exception ex)
        {
            WorkspaceLogger.LogWarning(ex, "Workspace scenario save failed for {Enterprise} FY {FiscalYear} Scenario={ScenarioName}.", SelectedEnterprise, SelectedFiscalYear, ActiveScenarioName);
            ScenarioPersistenceStatus = $"Scenario save failed: {ex.Message}";
            _apiHealth = WorkspaceApiHealth.Degraded;
        }
        finally
        {
            IsSavingScenario = false;
            StateHasChanged();
        }
    }

    protected async Task SaveWorkspaceBaselineAsync()
    {
        if (IsSavingBaseline)
        {
            return;
        }

        IsSavingBaseline = true;
        BaselineSaveStatus = "Saving baseline values to the workspace API...";
        StateHasChanged();

        try
        {
            var request = new WorkspaceBaselineUpdateRequest(
                SelectedEnterprise,
                SelectedFiscalYear,
                CurrentRate,
                TotalCosts,
                ProjectedVolume);

            var response = await WorkspaceSnapshotApiService.SaveWorkspaceBaselineAsync(request);
            WorkspaceState.ApplyBootstrap(response.Snapshot);
            BaselineSaveStatus = response.Message;
            WorkspaceLoadStatus = $"Reloaded {WorkspaceState.ContextSummary} after baseline save.";
            lastWorkspaceSyncUtc = DateTimeOffset.UtcNow;
            _apiHealth = WorkspaceApiHealth.Healthy;
            await RefreshScenarioCatalogAsync();
        }
        catch (Exception ex)
        {
            WorkspaceLogger.LogWarning(ex, "Workspace baseline save failed for {Enterprise} FY {FiscalYear}.", SelectedEnterprise, SelectedFiscalYear);
            BaselineSaveStatus = $"Baseline save failed: {ex.Message}";
            _apiHealth = WorkspaceApiHealth.Degraded;
        }
        finally
        {
            IsSavingBaseline = false;
            StateHasChanged();
        }
    }

    protected async Task ApplySelectedScenarioAsync()
    {
        if (IsApplyingScenario || SelectedScenarioSnapshotId is not > 0)
        {
            return;
        }

        IsApplyingScenario = true;
        ScenarioPersistenceStatus = "Applying saved scenario to the workspace...";
        StateHasChanged();

        try
        {
            var scenarioSnapshot = await WorkspaceSnapshotApiService.GetScenarioSnapshotAsync(SelectedScenarioSnapshotId.Value);
            WorkspaceState.ApplyBootstrap(scenarioSnapshot);
            ScenarioPersistenceStatus = $"Applied saved scenario '{WorkspaceState.ActiveScenarioName}'.";
            WorkspaceLoadStatus = $"Loaded {WorkspaceState.ContextSummary} from saved scenario.";
            lastWorkspaceSyncUtc = DateTimeOffset.UtcNow;
            _apiHealth = WorkspaceApiHealth.Healthy;
            await RefreshScenarioCatalogAsync(SelectedScenarioSnapshotId.Value);
        }
        catch (Exception ex)
        {
            WorkspaceLogger.LogWarning(ex, "Workspace scenario apply failed for {Enterprise} FY {FiscalYear} SnapshotId={SnapshotId}.", SelectedEnterprise, SelectedFiscalYear, SelectedScenarioSnapshotId);
            ScenarioPersistenceStatus = $"Scenario apply failed: {ex.Message}";
            _apiHealth = WorkspaceApiHealth.Degraded;
        }
        finally
        {
            IsApplyingScenario = false;
            StateHasChanged();
        }
    }

    protected async Task SaveRateSnapshotAsync()
    {
        if (IsSavingSnapshot)
        {
            return;
        }

        IsSavingSnapshot = true;
        SnapshotSaveStatus = "Saving rate snapshot to Aurora...";
        StateHasChanged();

        try
        {
            var savedSnapshot = await WorkspaceSnapshotApiService.SaveRateSnapshotAsync(WorkspaceState.ToBootstrapData());
            SnapshotSaveStatus = $"Saved {savedSnapshot.SnapshotName} at {savedSnapshot.SavedAtUtc}";
            lastWorkspaceSyncUtc = DateTimeOffset.UtcNow;
            _apiHealth = WorkspaceApiHealth.Healthy;
        }
        catch (Exception ex)
        {
            WorkspaceLogger.LogWarning(ex, "Workspace snapshot save failed for {Enterprise} FY {FiscalYear}.", SelectedEnterprise, SelectedFiscalYear);
            SnapshotSaveStatus = $"Snapshot save failed: {ex.Message}";
            _apiHealth = WorkspaceApiHealth.Degraded;
        }
        finally
        {
            IsSavingSnapshot = false;
            StateHasChanged();
        }
    }

    protected Task ExportCustomerWorkbookAsync()
    {
        return ExportDocumentAsync(
            () => WorkspaceDocumentExportService.CreateCustomerWorkbook(WorkspaceState),
            "Preparing customer workbook...");
    }

    protected Task ExportScenarioWorkbookAsync()
    {
        return ExportDocumentAsync(
            () => WorkspaceDocumentExportService.CreateScenarioWorkbook(WorkspaceState),
            "Preparing scenario workbook...");
    }

    protected Task ExportWorkspacePdfAsync()
    {
        return ExportDocumentAsync(
            () => WorkspaceDocumentExportService.CreateWorkspacePdfReport(WorkspaceState),
            "Preparing PDF rate packet...");
    }

    protected void HandleTotalCostsChanged(Syncfusion.Blazor.Inputs.ChangeEventArgs<decimal> args)
    {
        WorkspaceState.SetTotalCosts(args.Value);
    }

    protected void HandleProjectedVolumeChanged(Syncfusion.Blazor.Inputs.ChangeEventArgs<decimal> args)
    {
        WorkspaceState.SetProjectedVolume(args.Value);
    }

    protected void HandleCurrentRateChanged(Syncfusion.Blazor.Inputs.ChangeEventArgs<decimal> args)
    {
        WorkspaceState.SetCurrentRate(args.Value);
    }

    protected override void OnInitialized()
    {
        Console.WriteLine("[startup] WileyWorkspaceBase.OnInitialized entered.");
        WorkspaceLogger.LogInformation("Wiley workspace component initialized for panel {Panel}.", Panel ?? "overview");
        WorkspaceState.Changed += HandleWorkspaceStateChanged;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || persistenceInitialized)
        {
            return;
        }

        persistenceInitialized = true;
        Console.WriteLine("[startup] WileyWorkspaceBase.OnAfterRenderAsync first render.");
        WorkspaceLoadStatus = "Finalizing workspace state and restoring saved selections...";
        StateHasChanged();

        try
        {
            await WorkspacePersistenceService.InitializeAsync();
        }
        catch (Exception ex)
        {
            WorkspaceLogger.LogWarning(ex, "Workspace persistence initialization failed during first render.");
            WorkspaceLoadStatus = $"Workspace persistence initialization failed: {ex.Message}";
        }

        await RefreshScenarioCatalogAsync();

        if (!WorkspaceLoadStatus.Contains("failed", StringComparison.OrdinalIgnoreCase))
        {
            WorkspaceLoadStatus = BuildWorkspaceReadyStatus();
        }

        lastWorkspaceSyncUtc = DateTimeOffset.UtcNow;
        StateHasChanged();
    }

    public void Dispose()
    {
        WorkspaceState.Changed -= HandleWorkspaceStateChanged;
        WorkspacePersistenceService.Dispose();
        GC.SuppressFinalize(this);
    }

    private void HandleWorkspaceStateChanged()
    {
        if (string.IsNullOrWhiteSpace(SelectedEnterprise) || SelectedFiscalYear <= 0)
        {
            lastScenarioCatalogSelectionKey = null;
        }

        if (!string.IsNullOrWhiteSpace(SelectedEnterprise)
            && SelectedFiscalYear > 0
            && !IsLoadingWorkspace
            && !IsSavingBaseline
            && !IsApplyingScenario
            && !isRefreshingScenarioCatalog
            && !string.Equals($"{SelectedEnterprise.Trim().ToUpperInvariant()}::{SelectedFiscalYear}", lastScenarioCatalogSelectionKey, StringComparison.Ordinal))
        {
            _ = InvokeAsync(() => RefreshScenarioCatalogAsync(SelectedScenarioSnapshotId));
        }

        if (persistenceInitialized
            && !IsLoadingWorkspace
            && WorkspaceLoadStatus.StartsWith("Workspace ready.", StringComparison.Ordinal))
        {
            WorkspaceLoadStatus = BuildWorkspaceReadyStatus();
        }

        _ = InvokeAsync(StateHasChanged);
    }

    private string BuildWorkspaceReadyStatus()
    {
        if (IsUsingBrowserRestoredState && !string.IsNullOrWhiteSpace(CurrentStateSourceStatus))
        {
            return $"Workspace ready. {CurrentStateSourceStatus}";
        }

        if (!string.IsNullOrWhiteSpace(StartupSourceStatus)
            && !StartupSourceStatus.Contains("pending", StringComparison.OrdinalIgnoreCase))
        {
            return $"Workspace ready. {StartupSourceStatus}";
        }

        return "Workspace ready.";
    }

    private async Task ReloadWorkspaceAsync(string enterprise, int fiscalYear)
    {
        if (IsLoadingWorkspace)
        {
            return;
        }

        IsLoadingWorkspace = true;
        WorkspaceLoadStatus = $"Loading {enterprise} FY {fiscalYear} from the workspace API...";
        StateHasChanged();

        try
        {
            var snapshot = await WorkspaceSnapshotApiService.GetWorkspaceSnapshotAsync(enterprise, fiscalYear);
            WorkspaceState.ApplyBootstrap(snapshot);
            WorkspaceLoadStatus = $"Loaded {WorkspaceState.ContextSummary} from the workspace API.";
            lastWorkspaceSyncUtc = DateTimeOffset.UtcNow;
            _apiHealth = WorkspaceApiHealth.Healthy;
            await RefreshScenarioCatalogAsync();
        }
        catch (Exception ex)
        {
            WorkspaceLogger.LogWarning(ex, "Workspace reload failed for {Enterprise} FY {FiscalYear}.", enterprise, fiscalYear);
            WorkspaceLoadStatus = $"Workspace reload failed: {ex.Message}";
            _apiHealth = WorkspaceApiHealth.Degraded;
        }
        finally
        {
            IsLoadingWorkspace = false;
            StateHasChanged();
        }
    }

    private async Task RefreshScenarioCatalogAsync(long? selectedScenarioId = null)
    {
        if (string.IsNullOrWhiteSpace(SelectedEnterprise) || SelectedFiscalYear <= 0)
        {
            lastScenarioCatalogSelectionKey = null;
            SetNoSavedScenariosState("Saved scenarios are unavailable until the workspace reconnects to live data.");
            WorkspaceLogger.LogInformation("Saved scenario catalog refresh skipped because the workspace selection is not initialized (enterprise={Enterprise}, fiscalYear={FiscalYear}).", SelectedEnterprise, SelectedFiscalYear);
            return;
        }

        isRefreshingScenarioCatalog = true;
        var selectionKey = $"{SelectedEnterprise.Trim().ToUpperInvariant()}::{SelectedFiscalYear}";

        try
        {
            var scenarios = await WorkspaceSnapshotApiService.GetScenariosAsync(SelectedEnterprise, SelectedFiscalYear);
            SavedScenarios = scenarios.Scenarios;

            if (selectedScenarioId.HasValue && SavedScenarios.Any(item => item.SnapshotId == selectedScenarioId.Value))
            {
                SelectedScenarioSnapshotId = selectedScenarioId;
            }
            else if (SelectedScenarioSnapshotId.HasValue && SavedScenarios.All(item => item.SnapshotId != SelectedScenarioSnapshotId.Value))
            {
                SelectedScenarioSnapshotId = null;
            }

            if (SelectedScenarioSnapshotId.HasValue)
            {
                var selectedScenario = SavedScenarios.FirstOrDefault(item => item.SnapshotId == SelectedScenarioSnapshotId.Value);
                ScenarioDescription = selectedScenario?.Description ?? string.Empty;

                if (selectedScenario is not null)
                {
                    ScenarioPersistenceStatus = $"Selected saved scenario '{selectedScenario.ScenarioName}'.";
                }
            }
            else
            {
                ScenarioDescription = string.Empty;
            }

            if (SavedScenarios.Count == 0)
            {
                ScenarioPersistenceStatus = $"No saved scenarios found for {SelectedEnterprise} FY {SelectedFiscalYear}.";
            }
            else if (!SelectedScenarioSnapshotId.HasValue)
            {
                ScenarioPersistenceStatus = SavedScenarios.Count == 1
                    ? $"Loaded 1 saved scenario for {SelectedEnterprise} FY {SelectedFiscalYear}."
                    : $"Loaded {SavedScenarios.Count} saved scenarios for {SelectedEnterprise} FY {SelectedFiscalYear}.";
            }

            lastScenarioCatalogSelectionKey = selectionKey;
        }
        catch (Exception ex)
        {
            lastScenarioCatalogSelectionKey = selectionKey;
            WorkspaceLogger.LogWarning(ex, "Saved scenario catalog load failed for {Enterprise} FY {FiscalYear}. Falling back to no-scenarios mode.", SelectedEnterprise, SelectedFiscalYear);
            SetNoSavedScenariosState($"Saved scenarios are currently unavailable for {SelectedEnterprise} FY {SelectedFiscalYear}. You can keep working locally and try again when the API reconnects.");
            _apiHealth = WorkspaceApiHealth.Degraded;
        }
        finally
        {
            isRefreshingScenarioCatalog = false;
        }
    }

    private void SetNoSavedScenariosState(string status)
    {
        SavedScenarios = [];
        SelectedScenarioSnapshotId = null;
        ScenarioDescription = string.Empty;
        ScenarioPersistenceStatus = status;
    }

    private async Task ExportDocumentAsync(Func<WorkspaceExportDocument> exportFactory, string pendingStatus)
    {
        if (IsExportingDocuments)
        {
            return;
        }

        IsExportingDocuments = true;
        DocumentExportStatus = pendingStatus;
        StateHasChanged();

        try
        {
            var document = exportFactory();
            await BrowserDownloadService.DownloadAsync(document);
            DocumentExportStatus = $"Downloaded {document.FileName}";
        }
        catch (Exception ex)
        {
            DocumentExportStatus = $"Export failed: {ex.Message}";
        }
        finally
        {
            IsExportingDocuments = false;
            StateHasChanged();
        }
    }

    protected void HandleScenarioGridActionComplete(Syncfusion.Blazor.Grids.ActionEventArgs<ScenarioItem> args)
    {
        if (args.RequestType is Syncfusion.Blazor.Grids.Action.Save or Syncfusion.Blazor.Grids.Action.Delete)
        {
            WorkspaceState.Refresh();
        }
    }

    private static string NormalizePanelKey(string? value)
    {
        var candidate = string.IsNullOrWhiteSpace(value)
            ? "overview"
            : value.Trim().ToLowerInvariant();

        return candidate switch
        {
            "overview" => "overview",
            "break-even" => "break-even",
            "rates" => "rates",
            "quickbooks-import" => "quickbooks-import",
            "scenario" => "scenario",
            "customers" => "customers",
            "trends" => "trends",
            "decision-support" => "decision-support",
            "data-dashboard" => "data-dashboard",
            _ => "overview"
        };
    }

    protected sealed record WorkspacePanelNavItem(string Key, string Label);
}
#pragma warning restore S2325
