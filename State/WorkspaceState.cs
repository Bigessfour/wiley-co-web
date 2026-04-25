using WileyCoWeb.Contracts;

namespace WileyCoWeb.State;

public sealed class WorkspaceState
{
    private const string AllServicesOption = "All Services";
    private const string AllCityLimitsOption = "All";

    private string selectedEnterprise = WorkspaceDefaults.SelectedEnterprise;
    private int selectedFiscalYear = WorkspaceDefaults.SelectedFiscalYear;
    private string activeScenarioName = WorkspaceDefaults.ActiveScenarioName;
    private long? selectedScenarioSnapshotId;
    private string scenarioDescription = string.Empty;
    private string customerSearchTerm = string.Empty;
    private string selectedCustomerService = AllServicesOption;
    private string selectedCustomerCityLimits = AllCityLimitsOption;
    private decimal currentRate = WorkspaceDefaults.CurrentRate;
    private decimal totalCosts = WorkspaceDefaults.TotalCosts;
    private decimal projectedVolume = WorkspaceDefaults.ProjectedVolume;
    private string lastUpdatedUtc = DateTime.UtcNow.ToString("O");
    private readonly List<ScenarioItem> scenarioItems = [];
    private readonly List<CustomerRow> customerRows = [];
    private readonly List<ProjectionRow> projectionRows = [];
    private readonly List<BreakEvenQuadrantData> breakEvenQuadrants = [];
    private readonly List<ApartmentUnitTypeData> apartmentUnitTypes = [];
    private WorkspaceReserveTrajectoryData? reserveTrajectory;
    private readonly List<string> enterpriseOptions = [];
    private readonly List<int> fiscalYearOptions = [];
    private readonly List<string> customerServiceOptions = [AllServicesOption];
    private readonly List<string> customerCityLimitOptions = [AllCityLimitsOption, "Yes", "No"];
    private WorkspaceStartupSource startupSource;
    private string startupSourceStatus = "Workspace startup is pending.";
    private WorkspaceStartupSource currentStateSource;
    private string currentStateSourceStatus = "Current workspace state is pending initialization.";

    // ── Export timestamp tracking ─────────────────────────────────────────────
    // Keyed by export-type constants defined in WorkspaceExportKeys.
    // Values are ISO-8601 UTC strings so they survive JSON serialisation cleanly.
    private readonly Dictionary<string, string> lastExportedTimestamps = [];

    // ── Offline status (ephemeral — not persisted) ────────────────────────────
    // Set by WileyWorkspaceBase when navigator.onLine changes.  Drives the
    // offline-indicator banner in WileyWorkspace.razor.
    private bool isOffline;

    private Action? changed;

    public event Action? Changed
    {
        add
        {
            changed += value;
        }

        remove
        {
            changed -= value;
        }
    }

    public IReadOnlyList<string> EnterpriseOptions
    {
        get
        {
            return enterpriseOptions;
        }
    }

    public IReadOnlyList<int> FiscalYearOptions
    {
        get
        {
            return fiscalYearOptions;
        }
    }

    public WorkspaceStartupSource StartupSource
    {
        get
        {
            return startupSource;
        }
    }

    public string StartupSourceStatus
    {
        get
        {
            return startupSourceStatus;
        }
    }

    public bool IsUsingStartupFallback
    {
        get
        {
            return startupSource == WorkspaceStartupSource.LocalBootstrapFallback;
        }
    }

    public WorkspaceStartupSource CurrentStateSource
    {
        get
        {
            return currentStateSource;
        }
    }

    public string CurrentStateSourceStatus
    {
        get
        {
            return currentStateSourceStatus;
        }
    }

    public bool IsUsingBrowserRestoredState
    {
        get
        {
            return currentStateSource == WorkspaceStartupSource.BrowserStorageRestore;
        }
    }

    // ── Export timestamps ──────────────────────────────────────────────────────

    /// <summary>
    /// Snapshot of last-exported timestamps.  A copy is returned so callers
    /// cannot mutate the internal dictionary.  Use <see cref="SetLastExported"/>
    /// to record a new download and <see cref="GetLastExportedDisplay"/> for a
    /// localised human-readable label.
    /// </summary>
    public IReadOnlyDictionary<string, string> LastExportedTimestamps => lastExportedTimestamps;

    /// <summary>
    /// Returns a localised, human-readable label for the last time the given
    /// <paramref name="exportKey"/> was downloaded (e.g. "4/25/2026 2:15 PM"),
    /// or "Never" when no timestamp has been recorded.
    /// </summary>
    /// <param name="exportKey">
    /// One of the constants defined in <see cref="WorkspaceExportKeys"/>.
    /// </param>
    public string GetLastExportedDisplay(string exportKey)
    {
        if (!lastExportedTimestamps.TryGetValue(exportKey, out var iso)
            || string.IsNullOrWhiteSpace(iso))
        {
            return "Never";
        }

        return DateTimeOffset.TryParse(iso, out var dto)
            ? dto.ToLocalTime().ToString("g")
            : "Never";
    }

    /// <summary>
    /// Records the UTC timestamp for the given export type so the document
    /// centre can display when each file was last downloaded.  The timestamp
    /// is persisted to localStorage via <see cref="Services.WorkspacePersistenceService"/>
    /// on the next changed-event cycle.
    /// </summary>
    /// <param name="exportKey">
    /// One of the constants defined in <see cref="WorkspaceExportKeys"/>.
    /// </param>
    /// <param name="timestamp">
    /// The moment the download was initiated (should be
    /// <see cref="DateTimeOffset.UtcNow"/>).
    /// </param>
    public void SetLastExported(string exportKey, DateTimeOffset timestamp)
    {
        if (string.IsNullOrWhiteSpace(exportKey)) return;

        var iso = timestamp.UtcDateTime.ToString("O");
        if (lastExportedTimestamps.TryGetValue(exportKey, out var existing) && existing == iso) return;

        lastExportedTimestamps[exportKey] = iso;
        NotifyChanged();
    }

    // ── Offline status ─────────────────────────────────────────────────────────

    /// <summary>
    /// <c>true</c> when the browser reported that it has lost network access
    /// (<c>navigator.onLine == false</c>).  Set by WileyWorkspaceBase via
    /// <see cref="SetOffline"/> after the JS online/offline events fire.
    /// Not persisted — the real-time browser state is always authoritative.
    /// </summary>
    public bool IsOffline => isOffline;

    /// <summary>
    /// Updates the offline flag and notifies all state subscribers so the
    /// offline-indicator banner can appear or disappear immediately.
    /// </summary>
    public void SetOffline(bool offline)
    {
        if (isOffline == offline) return;
        isOffline = offline;
        NotifyChanged();
    }

    public string SelectedEnterprise
    {
        get
        {
            return selectedEnterprise;
        }
        set => SetSelection(value, selectedFiscalYear);
    }

    public int SelectedFiscalYear
    {
        get
        {
            return selectedFiscalYear;
        }
        set => SetSelection(selectedEnterprise, value);
    }

    public string ActiveScenarioName
    {
        get
        {
            return activeScenarioName;
        }
        set => SetActiveScenarioName(value);
    }

    public long? SelectedScenarioSnapshotId
    {
        get
        {
            return selectedScenarioSnapshotId;
        }
        set => SetSelectedScenarioSnapshotId(value);
    }

    public string ScenarioDescription
    {
        get
        {
            return scenarioDescription;
        }
        set => SetScenarioDescription(value);
    }

    public decimal CurrentRate
    {
        get
        {
            return currentRate;
        }
        set => SetCurrentRate(value);
    }

    public decimal TotalCosts
    {
        get
        {
            return totalCosts;
        }
        set => SetTotalCosts(value);
    }

    public decimal ProjectedVolume
    {
        get
        {
            return projectedVolume;
        }
        set => SetProjectedVolume(value);
    }

    public decimal RecommendedRate
    {
        get
        {
            return RateCalculator.CalculateRecommendedRate(TotalCosts, ProjectedVolume);
        }
    }

    public decimal RateDelta
    {
        get
        {
            return RateCalculator.CalculateRateDelta(CurrentRate, RecommendedRate);
        }
    }

    public decimal ScenarioCostTotal
    {
        get
        {
            return scenarioItems.Sum(item => item.Cost);
        }
    }

    public decimal AdjustedTotalCosts
    {
        get
        {
            return RateCalculator.CalculateAdjustedTotalCosts(TotalCosts, ScenarioCostTotal);
        }
    }

    public decimal AdjustedRecommendedRate
    {
        get
        {
            return RateCalculator.CalculateAdjustedRecommendedRate(AdjustedTotalCosts, ProjectedVolume);
        }
    }

    public decimal AdjustedRateDelta
    {
        get
        {
            return RateCalculator.CalculateAdjustedRateDelta(CurrentRate, AdjustedRecommendedRate);
        }
    }

    public string ContextSummary
    {
        get
        {
            return $"{SelectedEnterprise} FY {SelectedFiscalYear} | {ActiveScenarioName}";
        }
    }

    public IReadOnlyList<RateComparisonPoint> RateComparison
    {
        get
        {
            return RateCalculator.CreateRateComparison(CurrentRate, AdjustedRecommendedRate);
        }
    }

    public IReadOnlyList<ScenarioItem> ScenarioItems
    {
        get
        {
            return scenarioItems;
        }
    }

    public IReadOnlyList<CustomerRow> Customers
    {
        get
        {
            return customerRows;
        }
    }

    public IReadOnlyList<string> CustomerServiceOptions
    {
        get
        {
            return customerServiceOptions;
        }
    }

    public IReadOnlyList<string> CustomerCityLimitOptions
    {
        get
        {
            return customerCityLimitOptions;
        }
    }

    public string CustomerSearchTerm
    {
        get
        {
            return customerSearchTerm;
        }
        set
        {
            var normalizedValue = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
            if (!string.Equals(customerSearchTerm, normalizedValue, StringComparison.Ordinal))
            {
                customerSearchTerm = normalizedValue;
                NotifyChanged();
            }
        }
    }

    public string SelectedCustomerService
    {
        get
        {
            return selectedCustomerService;
        }
        set
        {
            var normalizedValue = string.IsNullOrWhiteSpace(value) ? AllServicesOption : value.Trim();
            if (!string.Equals(selectedCustomerService, normalizedValue, StringComparison.Ordinal))
            {
                selectedCustomerService = normalizedValue;
                NotifyChanged();
            }
        }
    }

    public string SelectedCustomerCityLimits
    {
        get
        {
            return selectedCustomerCityLimits;
        }
        set
        {
            var normalizedValue = string.IsNullOrWhiteSpace(value) ? AllCityLimitsOption : value.Trim();
            if (!string.Equals(selectedCustomerCityLimits, normalizedValue, StringComparison.Ordinal))
            {
                selectedCustomerCityLimits = normalizedValue;
                NotifyChanged();
            }
        }
    }

    public IReadOnlyList<CustomerRow> FilteredCustomers
    {
        get
        {
            return CustomerFilterService.FilterCustomers(
                Customers,
                customerSearchTerm,
                selectedCustomerService,
                selectedCustomerCityLimits);
        }
    }

    public int FilteredCustomerCount
    {
        get
        {
            return FilteredCustomers.Count;
        }
    }

    public IReadOnlyList<ProjectionRow> ProjectionSeries
    {
        get
        {
            return projectionRows;
        }
    }

    public IReadOnlyList<BreakEvenQuadrantData> BreakEvenQuadrants
    {
        get
        {
            return breakEvenQuadrants;
        }
    }

    public IReadOnlyList<ApartmentUnitTypeData> ApartmentUnitTypes
    {
        get
        {
            return apartmentUnitTypes;
        }
    }

    public WorkspaceReserveTrajectoryData? ReserveTrajectory
    {
        get
        {
            return reserveTrajectory;
        }
    }

    public void ApplyBootstrap(WorkspaceBootstrapData bootstrapData)
    {
        ArgumentNullException.ThrowIfNull(bootstrapData);

        var hasChanged = ApplyBootstrapSelection(bootstrapData)
            | ApplyBootstrapScalars(bootstrapData)
            | ApplyBootstrapScenarioMetadata(bootstrapData)
            | ApplyBootstrapOptions(bootstrapData)
            | ApplyBootstrapCollections(bootstrapData)
            | ApplyBootstrapLastUpdated(bootstrapData)
            // Restore last-exported timestamps so the document center shows
            // when each file was last downloaded, even after a page refresh.
            | ApplyBootstrapExportTimestamps(bootstrapData);

        if (hasChanged)
        {
            NotifyChanged();
        }
    }

    public WorkspaceBootstrapData ToBootstrapData()
    {
        return new WorkspaceBootstrapData(
            SelectedEnterprise,
            SelectedFiscalYear,
            ActiveScenarioName,
            CurrentRate,
            TotalCosts,
            ProjectedVolume,
            lastUpdatedUtc)
        {
            ScenarioItems = [.. scenarioItems.Select(item => new WorkspaceScenarioItemData(item.Id, item.Name, item.Cost))],
            EnterpriseOptions = [.. enterpriseOptions],
            FiscalYearOptions = [.. fiscalYearOptions],
            CustomerServiceOptions = [.. customerServiceOptions],
            CustomerCityLimitOptions = [.. customerCityLimitOptions],
            CustomerRows = [.. customerRows],
            ProjectionRows = [.. projectionRows],
            BreakEvenQuadrants = [.. breakEvenQuadrants],
            ApartmentUnitTypes = [.. apartmentUnitTypes],
            ReserveTrajectory = CloneReserveTrajectory(reserveTrajectory),
            SelectedScenarioSnapshotId = selectedScenarioSnapshotId,
            ScenarioDescription = string.IsNullOrWhiteSpace(scenarioDescription) ? null : scenarioDescription,
            // Persist export timestamps so the document center can show when each
            // file was last downloaded even after a full page refresh.
            LastExportedTimestamps = lastExportedTimestamps.Count > 0
                ? new Dictionary<string, string>(lastExportedTimestamps)
                : null
        };
    }

    public void SetSelection(string enterprise, int fiscalYear)
    {
        if (SetSelectionCore(enterprise, fiscalYear)) NotifyChanged();
    }

    public void SetActiveScenarioName(string scenarioName)
    {
        if (SetStringWithoutNotify(ref activeScenarioName, scenarioName, activeScenarioName)) NotifyChanged();
    }

    public void SetSelectedScenarioSnapshotId(long? snapshotId)
    {
        if (SetNullableLongField(ref selectedScenarioSnapshotId, snapshotId)) NotifyChanged();
    }

    public void SetScenarioDescription(string? description)
    {
        if (SetStringWithoutNotify(ref scenarioDescription, description, string.Empty)) NotifyChanged();
    }

    public void SetCurrentRate(decimal rate)
    {
        if (SetDecimalWithoutNotify(ref currentRate, rate, currentRate)) NotifyChanged();
    }

    public void SetTotalCosts(decimal costs)
    {
        if (SetDecimalWithoutNotify(ref totalCosts, costs, totalCosts)) NotifyChanged();
    }

    public void SetProjectedVolume(decimal volume)
    {
        if (SetDecimalWithoutNotify(ref projectedVolume, volume, projectedVolume)) NotifyChanged();
    }

    public void SetBreakEvenQuadrants(IReadOnlyList<BreakEvenQuadrantData> quadrants)
    {
        ArgumentNullException.ThrowIfNull(quadrants);

        if (SetBreakEvenQuadrantsWithoutNotify(quadrants)) NotifyChanged();
    }

    public void SetApartmentUnitTypes(IReadOnlyList<ApartmentUnitTypeData> unitTypes)
    {
        ArgumentNullException.ThrowIfNull(unitTypes);

        if (SetApartmentUnitTypesWithoutNotify(unitTypes)) NotifyChanged();
    }

    public void SetReserveTrajectory(WorkspaceReserveTrajectoryData? trajectory)
    {
        if (SetReserveTrajectoryWithoutNotify(trajectory)) NotifyChanged();
    }

    public void SetCustomerSearchTerm(string? searchTerm)
    {
        if (SetStringWithoutNotify(ref customerSearchTerm, searchTerm, string.Empty)) NotifyChanged();
    }

    public void SetCustomerServiceFilter(string? service)
    {
        if (SetStringWithoutNotify(ref selectedCustomerService, service, AllServicesOption)) NotifyChanged();
    }

    public void SetCustomerCityLimitsFilter(string? cityLimits)
    {
        if (SetStringWithoutNotify(ref selectedCustomerCityLimits, cityLimits, AllCityLimitsOption)) NotifyChanged();
    }

    public void ClearCustomerFilters()
    {
        if (TryClearCustomerFilters()) NotifyChanged();
    }

    public void ReplaceCustomerDirectory(IReadOnlyList<CustomerRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var normalizedRows = NormalizeCustomerDirectory(rows);
        var normalizedServiceOptions = BuildCustomerServiceOptions(normalizedRows);

        var hasChanged = SetCustomerRowsWithoutNotify(normalizedRows);
        hasChanged |= SetStringListWithoutNotify(customerServiceOptions, normalizedServiceOptions, [AllServicesOption]);
        hasChanged |= SetStringListWithoutNotify(customerCityLimitOptions, [AllCityLimitsOption, "Yes", "No"], [AllCityLimitsOption, "Yes", "No"]);

        hasChanged |= EnsureCustomerFilterSelectionsAreValid();

        if (hasChanged)
        {
            NotifyChanged();
        }
    }

    public void AddScenarioItem(string name, decimal cost)
    {
        var normalizedName = string.IsNullOrWhiteSpace(name) ? "Scenario item" : name.Trim();
        scenarioItems.Add(new ScenarioItem(normalizedName, cost));
        NotifyChanged();
    }

    public void UpdateScenarioItem(Guid id, string name, decimal cost)
    {
        var scenarioItem = scenarioItems.FirstOrDefault(item => item.Id == id);
        if (scenarioItem != null && TryUpdateScenarioItem(scenarioItem, name, cost)) NotifyChanged();
    }

    public void RemoveScenarioItem(Guid id)
    {
        var scenarioItem = scenarioItems.FirstOrDefault(item => item.Id == id);
        if (scenarioItem != null)
        {
            scenarioItems.Remove(scenarioItem);
            NotifyChanged();
        }
    }

    public void Refresh() => NotifyChanged();

    public void SetStartupSource(WorkspaceStartupSource source, string status)
    {
        if (SetStartupSourceCore(source, status)) NotifyChanged();
    }

    public void SetCurrentStateSource(WorkspaceStartupSource source, string status)
    {
        if (SetCurrentStateSourceCore(source, status)) NotifyChanged();
    }

    private bool SetSelectionWithoutNotify(string? enterprise, int fiscalYear)
    {
        return SetSelectionCore(enterprise, fiscalYear);
    }

    private static bool SetStringWithoutNotify(ref string field, string? value, string fallback)
        => SetStringField(ref field, NormalizeStringValue(value, fallback));

    private static bool SetDecimalWithoutNotify(ref decimal field, decimal? value, decimal fallback)
        => SetDecimalField(ref field, value ?? fallback);

    private bool SetScenarioItemsWithoutNotify(IReadOnlyList<WorkspaceScenarioItemData>? items)
    {
        var normalizedItems = NormalizeScenarioItems(items);
        var hasChanged = !scenarioItems.Select(item => (item.Id, item.Name, item.Cost)).SequenceEqual(normalizedItems.Select(item => (item.Id, item.Name, item.Cost)));
        scenarioItems.Clear();
        scenarioItems.AddRange(normalizedItems);
        return hasChanged;
    }

    private bool SetCustomerRowsWithoutNotify(IReadOnlyList<CustomerRow>? rows)
        => SetRowsWithoutNotify(customerRows, NormalizeCustomerRows(rows));

    private bool SetProjectionRowsWithoutNotify(IReadOnlyList<ProjectionRow>? rows)
        => SetRowsWithoutNotify(projectionRows, NormalizeProjectionRows(rows));

    private bool SetBreakEvenQuadrantsWithoutNotify(IReadOnlyList<BreakEvenQuadrantData>? rows)
        => SetRowsWithoutNotify(breakEvenQuadrants, NormalizeBreakEvenQuadrants(rows));

    private bool SetApartmentUnitTypesWithoutNotify(IReadOnlyList<ApartmentUnitTypeData>? rows)
        => SetRowsWithoutNotify(apartmentUnitTypes, NormalizeApartmentUnitTypes(rows));

    private bool SetReserveTrajectoryWithoutNotify(WorkspaceReserveTrajectoryData? trajectory)
    {
        var normalizedTrajectory = NormalizeReserveTrajectory(trajectory);
        var hasChanged = !ReserveTrajectoryEquals(reserveTrajectory, normalizedTrajectory);
        reserveTrajectory = normalizedTrajectory;
        return hasChanged;
    }

    private static List<CustomerRow> NormalizeCustomerRows(IReadOnlyList<CustomerRow>? rows) => rows?.Select(row => new CustomerRow(row.Name, row.Service, row.CityLimits)).ToList() ?? [];

    private static List<ProjectionRow> NormalizeProjectionRows(IReadOnlyList<ProjectionRow>? rows) => rows?.Select(row => new ProjectionRow(row.Year, row.Rate)).ToList() ?? [];

    private static List<BreakEvenQuadrantData> NormalizeBreakEvenQuadrants(IReadOnlyList<BreakEvenQuadrantData>? rows)
        => rows?.Select(row => new BreakEvenQuadrantData(
            row.EnterpriseName,
            row.EnterpriseType,
            row.CurrentRate,
            row.MonthlyExpenses,
            row.MonthlyRevenue,
            row.MonthlyBalance,
            row.BreakEvenRate,
            row.EffectiveCustomerCount,
            row.SeriesPoints.Select(point => new BreakEvenSeriesPoint(point.PeriodLabel, point.RevenuePerCustomer, point.ExpensesPerCustomer, point.BreakEvenPerCustomer)).ToList())).ToList() ?? [];

    private static List<ApartmentUnitTypeData> NormalizeApartmentUnitTypes(IReadOnlyList<ApartmentUnitTypeData>? rows)
        => rows?.Select(row => new ApartmentUnitTypeData(row.Id, row.Name, row.BedroomCount, row.UnitCount, row.MonthlyRent)).ToList() ?? [];

    private static WorkspaceReserveTrajectoryData? NormalizeReserveTrajectory(WorkspaceReserveTrajectoryData? trajectory)
    {
        if (trajectory is null)
        {
            return null;
        }

        return new WorkspaceReserveTrajectoryData(
            trajectory.CurrentReserves,
            trajectory.RecommendedReserveLevel,
            NormalizeStringValue(trajectory.RiskAssessment, string.Empty),
            trajectory.ForecastPoints?.Select(point => new WorkspaceReserveTrajectoryPointData(point.DateUtc, point.PredictedReserves, point.ConfidenceInterval)).ToList() ?? []);
    }

    private static WorkspaceReserveTrajectoryData? CloneReserveTrajectory(WorkspaceReserveTrajectoryData? trajectory)
        => NormalizeReserveTrajectory(trajectory);

    private static bool ReserveTrajectoryEquals(WorkspaceReserveTrajectoryData? left, WorkspaceReserveTrajectoryData? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }

        return left.CurrentReserves == right.CurrentReserves
            && left.RecommendedReserveLevel == right.RecommendedReserveLevel
            && string.Equals(left.RiskAssessment, right.RiskAssessment, StringComparison.Ordinal)
            && left.ForecastPoints.SequenceEqual(right.ForecastPoints);
    }

    private static IReadOnlyList<string> NormalizeStringList(IReadOnlyList<string>? source, IReadOnlyList<string> fallback) => (source is { Count: > 0 } ? source : fallback).Where(item => !string.IsNullOrWhiteSpace(item)).Select(item => item.Trim()).Distinct(StringComparer.Ordinal).ToList();

    private static IReadOnlyList<int> NormalizeIntList(IReadOnlyList<int>? source, IReadOnlyList<int> fallback) => (source is { Count: > 0 } ? source : fallback).Where(item => item > 0).Distinct().OrderBy(item => item).ToList();

    private static bool SetStringListWithoutNotify(List<string> target, IReadOnlyList<string>? source, IReadOnlyList<string> fallback)
        => SetRowsWithoutNotify(target, NormalizeStringList(source, fallback));

    private static bool SetIntListWithoutNotify(List<int> target, IReadOnlyList<int>? source, IReadOnlyList<int> fallback)
        => SetRowsWithoutNotify(target, NormalizeIntList(source, fallback));

    private static bool SetRowsWithoutNotify<T>(List<T> target, IReadOnlyList<T> normalizedRows) { var hasChanged = !target.SequenceEqual(normalizedRows); target.Clear(); target.AddRange(normalizedRows); return hasChanged; }

    private static string NormalizeStringValue(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static bool SetStringField(ref string field, string value)
    {
        var hasChanged = !string.Equals(field, value, StringComparison.Ordinal);
        field = value;
        return hasChanged;
    }

    private static bool SetDecimalField(ref decimal field, decimal value)
    {
        var hasChanged = field != value;
        field = value;
        return hasChanged;
    }

    private static bool SetIntField(ref int field, int value)
    {
        var hasChanged = field != value;
        field = value;
        return hasChanged;
    }

    private static bool SetNullableLongField(ref long? field, long? value)
    {
        var hasChanged = field != value;
        field = value;
        return hasChanged;
    }

    private static bool SetEnumField<TEnum>(ref TEnum field, TEnum value)
        where TEnum : struct, Enum
    {
        var hasChanged = !EqualityComparer<TEnum>.Default.Equals(field, value);
        field = value;
        return hasChanged;
    }

    private bool SetSelectionCore(string? enterprise, int fiscalYear) => SetStringField(ref selectedEnterprise, NormalizeSelectionEnterprise(enterprise)) | SetIntField(ref selectedFiscalYear, NormalizeSelectionFiscalYear(fiscalYear));

    private bool TryClearCustomerFilters()
        => ClearCustomerSearchTerm()
            | ClearCustomerServiceFilter()
            | ClearCustomerCityLimitsFilter();

    private static bool TryUpdateScenarioItem(ScenarioItem scenarioItem, string name, decimal cost) { var normalizedName = NormalizeStringValue(name, scenarioItem.Name); var hasChanged = !string.Equals(scenarioItem.Name, normalizedName, StringComparison.Ordinal) || scenarioItem.Cost != cost; scenarioItem.Name = normalizedName; scenarioItem.Cost = cost; return hasChanged; }

    private bool SetStartupSourceCore(WorkspaceStartupSource source, string status) { var normalizedStatus = NormalizeStartupSourceStatus(status); var hasChanged = SetEnumField(ref startupSource, source) | SetStringField(ref startupSourceStatus, normalizedStatus); InitializeCurrentStateSource(source, normalizedStatus); return hasChanged; }

    private bool SetCurrentStateSourceCore(WorkspaceStartupSource source, string status) => SetEnumField(ref currentStateSource, source) | SetStringField(ref currentStateSourceStatus, NormalizeStartupSourceStatus(status));

    private static List<ScenarioItem> NormalizeScenarioItems(IReadOnlyList<WorkspaceScenarioItemData>? items) => items?.Select(item => new ScenarioItem(item.Id, item.Name, item.Cost)).ToList() ?? [];

    private bool ApplyBootstrapSelection(WorkspaceBootstrapData bootstrapData)
        => SetSelectionWithoutNotify(bootstrapData.SelectedEnterprise, bootstrapData.SelectedFiscalYear);

    private bool ApplyBootstrapScalars(WorkspaceBootstrapData bootstrapData)
        => SetStringWithoutNotify(ref activeScenarioName, bootstrapData.ActiveScenarioName, WorkspaceDefaults.ActiveScenarioName)
            | SetDecimalWithoutNotify(ref currentRate, bootstrapData.CurrentRate, WorkspaceDefaults.CurrentRate)
            | SetDecimalWithoutNotify(ref totalCosts, bootstrapData.TotalCosts, WorkspaceDefaults.TotalCosts)
            | SetDecimalWithoutNotify(ref projectedVolume, bootstrapData.ProjectedVolume, WorkspaceDefaults.ProjectedVolume);

    private bool ApplyBootstrapScenarioMetadata(WorkspaceBootstrapData bootstrapData)
        => SetNullableLongField(ref selectedScenarioSnapshotId, bootstrapData.SelectedScenarioSnapshotId)
            | SetStringWithoutNotify(ref scenarioDescription, bootstrapData.ScenarioDescription, string.Empty);

    private bool ApplyBootstrapOptions(WorkspaceBootstrapData bootstrapData)
        => SetStringListWithoutNotify(enterpriseOptions, bootstrapData.EnterpriseOptions, WorkspaceDefaults.EnterpriseOptions)
            | SetIntListWithoutNotify(fiscalYearOptions, bootstrapData.FiscalYearOptions, WorkspaceDefaults.FiscalYearOptions)
            | SetStringListWithoutNotify(customerServiceOptions, bootstrapData.CustomerServiceOptions, WorkspaceDefaults.CustomerServiceOptions)
            | SetStringListWithoutNotify(customerCityLimitOptions, bootstrapData.CustomerCityLimitOptions, WorkspaceDefaults.CustomerCityLimitOptions);

    private bool ApplyBootstrapCollections(WorkspaceBootstrapData bootstrapData)
        => SetScenarioItemsWithoutNotify(bootstrapData.ScenarioItems)
            | SetCustomerRowsWithoutNotify(bootstrapData.CustomerRows)
            | SetProjectionRowsWithoutNotify(bootstrapData.ProjectionRows)
            | SetBreakEvenQuadrantsWithoutNotify(bootstrapData.BreakEvenQuadrants)
            | SetApartmentUnitTypesWithoutNotify(bootstrapData.ApartmentUnitTypes)
            | SetReserveTrajectoryWithoutNotify(bootstrapData.ReserveTrajectory);

    private bool ApplyBootstrapLastUpdated(WorkspaceBootstrapData bootstrapData)
        => SetStringWithoutNotify(ref lastUpdatedUtc, bootstrapData.LastUpdatedUtc, lastUpdatedUtc);

    /// <summary>
    /// Merges persisted last-exported timestamps from <paramref name="bootstrapData"/>
    /// into the live dictionary.  Existing entries are only replaced when the
    /// persisted timestamp is newer than the in-memory one.  This ensures that
    /// a fresh export performed before a reload is never clobbered by a stale
    /// stored value.
    /// </summary>
    private bool ApplyBootstrapExportTimestamps(WorkspaceBootstrapData bootstrapData)
    {
        if (bootstrapData.LastExportedTimestamps is not { Count: > 0 }) return false;

        var hasChanged = false;
        foreach (var kvp in bootstrapData.LastExportedTimestamps)
        {
            if (string.IsNullOrWhiteSpace(kvp.Key) || string.IsNullOrWhiteSpace(kvp.Value)) continue;

            // Only update if the entry is new or the stored timestamp is newer.
            if (!lastExportedTimestamps.TryGetValue(kvp.Key, out var existing)
                || (DateTimeOffset.TryParse(kvp.Value, out var incoming)
                    && DateTimeOffset.TryParse(existing, out var current)
                    && incoming > current))
            {
                lastExportedTimestamps[kvp.Key] = kvp.Value;
                hasChanged = true;
            }
        }

        return hasChanged;
    }

    private static List<CustomerRow> NormalizeCustomerDirectory(IReadOnlyList<CustomerRow> rows) => rows.Where(row => !string.IsNullOrWhiteSpace(row.Name)).Select(row => new CustomerRow(row.Name.Trim(), string.IsNullOrWhiteSpace(row.Service) ? "Unknown" : row.Service.Trim(), string.IsNullOrWhiteSpace(row.CityLimits) ? "No" : row.CityLimits.Trim())).ToList();

    private static List<string> BuildCustomerServiceOptions(IReadOnlyList<CustomerRow> rows) => [AllServicesOption, .. rows.Select(row => row.Service).Distinct(StringComparer.Ordinal).OrderBy(service => service, StringComparer.Ordinal)];

    private bool EnsureCustomerFilterSelectionsAreValid() => SetValidatedSelection(customerServiceOptions, selectedCustomerService, AllServicesOption, value => selectedCustomerService = value) | SetValidatedSelection(customerCityLimitOptions, selectedCustomerCityLimits, AllCityLimitsOption, value => selectedCustomerCityLimits = value);

    private static bool SetValidatedSelection<T>(IReadOnlyCollection<T> options, T selectedValue, T fallbackValue, Action<T> assignValue) where T : notnull { var normalizedValue = options.Contains(selectedValue) ? selectedValue : fallbackValue; var hasChanged = !EqualityComparer<T>.Default.Equals(selectedValue, normalizedValue); assignValue(normalizedValue); return hasChanged; }

    private string NormalizeSelectionEnterprise(string? enterprise)
        => string.IsNullOrWhiteSpace(enterprise) ? selectedEnterprise : enterprise.Trim();

    private int NormalizeSelectionFiscalYear(int fiscalYear)
        => fiscalYear <= 0 ? selectedFiscalYear : fiscalYear;

    private bool ClearCustomerSearchTerm()
        => SetStringField(ref customerSearchTerm, string.Empty);

    private bool ClearCustomerServiceFilter()
        => SetStringField(ref selectedCustomerService, AllServicesOption);

    private bool ClearCustomerCityLimitsFilter()
        => SetStringField(ref selectedCustomerCityLimits, AllCityLimitsOption);

    private static string NormalizeStartupSourceStatus(string status)
        => string.IsNullOrWhiteSpace(status) ? "Workspace startup completed." : status.Trim();

    private void InitializeCurrentStateSource(WorkspaceStartupSource source, string normalizedStatus) { var isUninitialized = currentStateSource == WorkspaceStartupSource.None; currentStateSource = isUninitialized ? source : currentStateSource; currentStateSourceStatus = isUninitialized ? normalizedStatus : currentStateSourceStatus; }

    private void NotifyChanged() => changed?.Invoke();

}

public sealed record RateComparisonPoint(string Label, double Value);

public enum WorkspaceStartupSource
{
    None = 0,
    ApiSnapshot = 1,
    LocalBootstrapFallback = 2,
    BrowserStorageRestore = 3
}

public sealed class ScenarioItem
{
    public ScenarioItem()
    {
        Id = Guid.NewGuid();
    }

    public ScenarioItem(string name, decimal cost) : this(Guid.NewGuid(), name, cost)
    {
    }

    public ScenarioItem(Guid id, string name, decimal cost)
    {
        Id = id;
        Name = name;
        Cost = cost;
    }

    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public decimal Cost { get; set; }
}