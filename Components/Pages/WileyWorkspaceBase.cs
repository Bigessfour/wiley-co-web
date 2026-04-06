using Microsoft.AspNetCore.Components;
using System.Globalization;
using WileyCoWeb.Services;
using WileyCoWeb.State;

namespace WileyCoWeb.Components.Pages;

#pragma warning disable S2325
public partial class WileyWorkspaceBase : ComponentBase, IDisposable
{
    [Inject]
    protected WorkspaceState WorkspaceState { get; set; } = default!;

    [Inject]
    protected WorkspacePersistenceService WorkspacePersistenceService { get; set; } = default!;

    [Inject]
    protected WorkspaceSnapshotApiService WorkspaceSnapshotApiService { get; set; } = default!;

    [Inject]
    protected WorkspaceBootstrapService WorkspaceBootstrapService { get; set; } = default!;

    private bool persistenceInitialized;

    protected bool IsSavingSnapshot { get; set; }

    protected bool IsLoadingSnapshot { get; set; }

    protected bool IsScenarioSnapshotBusy => IsSavingSnapshot || IsLoadingSnapshot;

    protected string SnapshotSaveStatus { get; set; } = "Ready to save or load scenario snapshots";

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

    protected string CurrentRateInputText
    {
        get => CurrentRate.ToString("0.##", CultureInfo.CurrentCulture);
        set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            if (decimal.TryParse(value, NumberStyles.Currency, CultureInfo.CurrentCulture, out var parsedRate))
            {
                WorkspaceState.SetCurrentRate(parsedRate);
            }
        }
    }

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

    protected void ClearCustomerFilters() => WorkspaceState.ClearCustomerFilters();

    protected async Task SaveRateSnapshotAsync()
    {
        if (IsScenarioSnapshotBusy)
        {
            return;
        }

        IsSavingSnapshot = true;
        SnapshotSaveStatus = "Saving scenario snapshot to Aurora...";
        StateHasChanged();

        try
        {
            var savedSnapshot = await WorkspaceSnapshotApiService.SaveRateSnapshotAsync(WorkspaceState.ToBootstrapData());
            SnapshotSaveStatus = $"Saved {savedSnapshot.SnapshotName} at {savedSnapshot.SavedAtUtc}";
        }
        catch (Exception ex)
        {
            SnapshotSaveStatus = $"Snapshot save failed: {ex.Message}";
        }
        finally
        {
            IsSavingSnapshot = false;
            StateHasChanged();
        }
    }

    protected async Task LoadScenarioSnapshotAsync()
    {
        if (IsScenarioSnapshotBusy)
        {
            return;
        }

        var enterprise = SelectedEnterprise;
        var fiscalYear = SelectedFiscalYear;

        IsLoadingSnapshot = true;
        SnapshotSaveStatus = $"Loading scenario snapshot for {enterprise} FY{fiscalYear}...";
        StateHasChanged();

        try
        {
            await WorkspaceBootstrapService.LoadAsync(enterprise, fiscalYear);
            SnapshotSaveStatus = $"Loaded scenario snapshot for {WorkspaceState.ContextSummary}";
        }
        catch (Exception ex)
        {
            SnapshotSaveStatus = $"Scenario load failed: {ex.Message}";
        }
        finally
        {
            IsLoadingSnapshot = false;
            StateHasChanged();
        }
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
        WorkspaceState.Changed += HandleWorkspaceStateChanged;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || persistenceInitialized)
        {
            return;
        }

        persistenceInitialized = true;
        await WorkspacePersistenceService.InitializeAsync();
    }

    public void Dispose()
    {
        WorkspaceState.Changed -= HandleWorkspaceStateChanged;
        WorkspacePersistenceService.Dispose();
    }

    private void HandleWorkspaceStateChanged()
    {
        _ = WorkspaceState;
        _ = InvokeAsync(StateHasChanged);
    }

    protected void HandleScenarioGridActionComplete(Syncfusion.Blazor.Grids.ActionEventArgs<ScenarioItem> args)
    {
        if (args.RequestType is Syncfusion.Blazor.Grids.Action.Save or Syncfusion.Blazor.Grids.Action.Delete)
        {
            WorkspaceState.Refresh();
        }
    }
}
#pragma warning restore S2325