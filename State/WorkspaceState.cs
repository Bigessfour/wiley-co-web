using WileyCoWeb.Contracts;

namespace WileyCoWeb.State;

public sealed class WorkspaceState
{
    private const string AllServicesOption = "All Services";
    private const string AllCityLimitsOption = "All";

    private string selectedEnterprise = WorkspaceDefaults.SelectedEnterprise;
    private int selectedFiscalYear = WorkspaceDefaults.SelectedFiscalYear;
    private string activeScenarioName = WorkspaceDefaults.ActiveScenarioName;
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
    private readonly List<string> enterpriseOptions = [];
    private readonly List<int> fiscalYearOptions = [];
    private readonly List<string> customerServiceOptions = [AllServicesOption];
    private readonly List<string> customerCityLimitOptions = [AllCityLimitsOption, "Yes", "No"];
    private WorkspaceStartupSource startupSource;
    private string startupSourceStatus = "Workspace startup is pending.";
    private WorkspaceStartupSource currentStateSource;
    private string currentStateSourceStatus = "Current workspace state is pending initialization.";

    private Action? changed;

    public event Action? Changed
    {
        add => changed += value;
        remove => changed -= value;
    }

    public IReadOnlyList<string> EnterpriseOptions => enterpriseOptions;

    public IReadOnlyList<int> FiscalYearOptions => fiscalYearOptions;

    public WorkspaceStartupSource StartupSource => startupSource;

    public string StartupSourceStatus => startupSourceStatus;

    public bool IsUsingStartupFallback => startupSource == WorkspaceStartupSource.LocalBootstrapFallback;

    public WorkspaceStartupSource CurrentStateSource => currentStateSource;

    public string CurrentStateSourceStatus => currentStateSourceStatus;

    public bool IsUsingBrowserRestoredState => currentStateSource == WorkspaceStartupSource.BrowserStorageRestore;

    public string SelectedEnterprise
    {
        get => selectedEnterprise;
        set => SetSelection(value, selectedFiscalYear);
    }

    public int SelectedFiscalYear
    {
        get => selectedFiscalYear;
        set => SetSelection(selectedEnterprise, value);
    }

    public string ActiveScenarioName
    {
        get => activeScenarioName;
        set => SetActiveScenarioName(value);
    }

    public decimal CurrentRate
    {
        get => currentRate;
        set => SetCurrentRate(value);
    }

    public decimal TotalCosts
    {
        get => totalCosts;
        set => SetTotalCosts(value);
    }

    public decimal ProjectedVolume
    {
        get => projectedVolume;
        set => SetProjectedVolume(value);
    }

    public decimal RecommendedRate => ProjectedVolume == 0 ? 0 : TotalCosts / ProjectedVolume;

    public decimal RateDelta => CurrentRate - RecommendedRate;

    public decimal ScenarioCostTotal => scenarioItems.Sum(item => item.Cost);

    public decimal AdjustedTotalCosts => TotalCosts + ScenarioCostTotal;

    public decimal AdjustedRecommendedRate => ProjectedVolume == 0 ? 0 : AdjustedTotalCosts / ProjectedVolume;

    public decimal AdjustedRateDelta => CurrentRate - AdjustedRecommendedRate;

    public string ContextSummary => $"{SelectedEnterprise} FY {SelectedFiscalYear} | {ActiveScenarioName}";

    public IReadOnlyList<RateComparisonPoint> RateComparison =>
    [
        new RateComparisonPoint("Current", (double)CurrentRate),
        new RateComparisonPoint("Break-Even", (double)AdjustedRecommendedRate)
    ];

    public IReadOnlyList<ScenarioItem> ScenarioItems => scenarioItems;

    public IReadOnlyList<CustomerRow> Customers => customerRows;

    public IReadOnlyList<string> CustomerServiceOptions => customerServiceOptions;

    public IReadOnlyList<string> CustomerCityLimitOptions => customerCityLimitOptions;

    public string CustomerSearchTerm
    {
        get => customerSearchTerm;
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
        get => selectedCustomerService;
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
        get => selectedCustomerCityLimits;
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

    public IReadOnlyList<CustomerRow> FilteredCustomers =>
    [
        .. Customers.Where(customer =>
            (string.IsNullOrWhiteSpace(customerSearchTerm) ||
             customer.Name.Contains(customerSearchTerm, StringComparison.OrdinalIgnoreCase) ||
             customer.Service.Contains(customerSearchTerm, StringComparison.OrdinalIgnoreCase) ||
             customer.CityLimits.Contains(customerSearchTerm, StringComparison.OrdinalIgnoreCase)) &&
            (string.Equals(selectedCustomerService, AllServicesOption, StringComparison.Ordinal) ||
             string.Equals(customer.Service, selectedCustomerService, StringComparison.Ordinal)) &&
            (string.Equals(selectedCustomerCityLimits, AllCityLimitsOption, StringComparison.Ordinal) ||
             string.Equals(customer.CityLimits, selectedCustomerCityLimits, StringComparison.Ordinal)))
    ];

    public int FilteredCustomerCount => FilteredCustomers.Count;

    public IReadOnlyList<ProjectionRow> ProjectionSeries => projectionRows;

    public void ApplyBootstrap(WorkspaceBootstrapData bootstrapData)
    {
        ArgumentNullException.ThrowIfNull(bootstrapData);

        var hasChanged = false;

        hasChanged |= SetSelectionWithoutNotify(bootstrapData.SelectedEnterprise, bootstrapData.SelectedFiscalYear);
        hasChanged |= SetStringWithoutNotify(ref activeScenarioName, bootstrapData.ActiveScenarioName, WorkspaceDefaults.ActiveScenarioName);
        hasChanged |= SetDecimalWithoutNotify(ref currentRate, bootstrapData.CurrentRate, WorkspaceDefaults.CurrentRate);
        hasChanged |= SetDecimalWithoutNotify(ref totalCosts, bootstrapData.TotalCosts, WorkspaceDefaults.TotalCosts);
        hasChanged |= SetDecimalWithoutNotify(ref projectedVolume, bootstrapData.ProjectedVolume, WorkspaceDefaults.ProjectedVolume);
        hasChanged |= SetStringListWithoutNotify(enterpriseOptions, bootstrapData.EnterpriseOptions, WorkspaceDefaults.EnterpriseOptions);
        hasChanged |= SetIntListWithoutNotify(fiscalYearOptions, bootstrapData.FiscalYearOptions, WorkspaceDefaults.FiscalYearOptions);
        hasChanged |= SetStringListWithoutNotify(customerServiceOptions, bootstrapData.CustomerServiceOptions, WorkspaceDefaults.CustomerServiceOptions);
        hasChanged |= SetStringListWithoutNotify(customerCityLimitOptions, bootstrapData.CustomerCityLimitOptions, WorkspaceDefaults.CustomerCityLimitOptions);
        hasChanged |= SetScenarioItemsWithoutNotify(bootstrapData.ScenarioItems);
        hasChanged |= SetCustomerRowsWithoutNotify(bootstrapData.CustomerRows);
        hasChanged |= SetProjectionRowsWithoutNotify(bootstrapData.ProjectionRows);

        if (!string.IsNullOrWhiteSpace(bootstrapData.LastUpdatedUtc))
            lastUpdatedUtc = bootstrapData.LastUpdatedUtc;

        if (hasChanged)
        {
            NotifyChanged();
        }
    }

    public WorkspaceBootstrapData ToBootstrapData() => new(
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
        ProjectionRows = [.. projectionRows]
    };

    public void SetSelection(string enterprise, int fiscalYear)
    {
        var normalizedEnterprise = string.IsNullOrWhiteSpace(enterprise) ? selectedEnterprise : enterprise.Trim();
        var normalizedFiscalYear = fiscalYear <= 0 ? selectedFiscalYear : fiscalYear;

        if (string.Equals(selectedEnterprise, normalizedEnterprise, StringComparison.Ordinal) &&
            selectedFiscalYear == normalizedFiscalYear)
        {
            return;
        }

        selectedEnterprise = normalizedEnterprise;
        selectedFiscalYear = normalizedFiscalYear;
        NotifyChanged();
    }

    public void SetActiveScenarioName(string scenarioName)
    {
        var normalizedScenarioName = string.IsNullOrWhiteSpace(scenarioName) ? activeScenarioName : scenarioName.Trim();
        if (string.Equals(activeScenarioName, normalizedScenarioName, StringComparison.Ordinal))
        {
            return;
        }

        activeScenarioName = normalizedScenarioName;
        NotifyChanged();
    }

    public void SetCurrentRate(decimal rate)
    {
        if (currentRate == rate)
        {
            return;
        }

        currentRate = rate;
        NotifyChanged();
    }

    public void SetTotalCosts(decimal costs)
    {
        if (totalCosts == costs)
        {
            return;
        }

        totalCosts = costs;
        NotifyChanged();
    }

    public void SetProjectedVolume(decimal volume)
    {
        if (projectedVolume == volume)
        {
            return;
        }

        projectedVolume = volume;
        NotifyChanged();
    }

    public void SetCustomerSearchTerm(string? searchTerm)
    {
        var normalizedSearchTerm = string.IsNullOrWhiteSpace(searchTerm) ? string.Empty : searchTerm.Trim();
        if (string.Equals(customerSearchTerm, normalizedSearchTerm, StringComparison.Ordinal))
        {
            return;
        }

        customerSearchTerm = normalizedSearchTerm;
        NotifyChanged();
    }

    public void SetCustomerServiceFilter(string? service)
    {
        var normalizedService = string.IsNullOrWhiteSpace(service) ? AllServicesOption : service.Trim();
        if (string.Equals(selectedCustomerService, normalizedService, StringComparison.Ordinal))
        {
            return;
        }

        selectedCustomerService = normalizedService;
        NotifyChanged();
    }

    public void SetCustomerCityLimitsFilter(string? cityLimits)
    {
        var normalizedCityLimits = string.IsNullOrWhiteSpace(cityLimits) ? AllCityLimitsOption : cityLimits.Trim();
        if (string.Equals(selectedCustomerCityLimits, normalizedCityLimits, StringComparison.Ordinal))
        {
            return;
        }

        selectedCustomerCityLimits = normalizedCityLimits;
        NotifyChanged();
    }

    public void ClearCustomerFilters()
    {
        var hasChanged = false;

        if (!string.IsNullOrEmpty(customerSearchTerm))
        {
            customerSearchTerm = string.Empty;
            hasChanged = true;
        }

        if (!string.Equals(selectedCustomerService, AllServicesOption, StringComparison.Ordinal))
        {
            selectedCustomerService = AllServicesOption;
            hasChanged = true;
        }

        if (!string.Equals(selectedCustomerCityLimits, AllCityLimitsOption, StringComparison.Ordinal))
        {
            selectedCustomerCityLimits = AllCityLimitsOption;
            hasChanged = true;
        }

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
        if (scenarioItem == null)
        {
            return;
        }

        var normalizedName = string.IsNullOrWhiteSpace(name) ? scenarioItem.Name : name.Trim();
        var hasChanged = !string.Equals(scenarioItem.Name, normalizedName, StringComparison.Ordinal) || scenarioItem.Cost != cost;

        scenarioItem.Name = normalizedName;
        scenarioItem.Cost = cost;

        if (hasChanged)
        {
            NotifyChanged();
        }
    }

    public void RemoveScenarioItem(Guid id)
    {
        var scenarioItem = scenarioItems.FirstOrDefault(item => item.Id == id);
        if (scenarioItem == null)
        {
            return;
        }

        scenarioItems.Remove(scenarioItem);
        NotifyChanged();
    }

    public void Refresh() => NotifyChanged();

    public void SetStartupSource(WorkspaceStartupSource source, string status)
    {
        var normalizedStatus = string.IsNullOrWhiteSpace(status) ? "Workspace startup completed." : status.Trim();
        if (startupSource == source && string.Equals(startupSourceStatus, normalizedStatus, StringComparison.Ordinal))
        {
            return;
        }

        startupSource = source;
        startupSourceStatus = normalizedStatus;

        if (currentStateSource == WorkspaceStartupSource.None)
        {
            currentStateSource = source;
            currentStateSourceStatus = normalizedStatus;
        }

        NotifyChanged();
    }

    public void SetCurrentStateSource(WorkspaceStartupSource source, string status)
    {
        var normalizedStatus = string.IsNullOrWhiteSpace(status) ? "Workspace state is ready." : status.Trim();
        if (currentStateSource == source && string.Equals(currentStateSourceStatus, normalizedStatus, StringComparison.Ordinal))
        {
            return;
        }

        currentStateSource = source;
        currentStateSourceStatus = normalizedStatus;
        NotifyChanged();
    }

    private bool SetSelectionWithoutNotify(string? enterprise, int fiscalYear)
    {
        var normalizedEnterprise = string.IsNullOrWhiteSpace(enterprise) ? selectedEnterprise : enterprise.Trim();
        var normalizedFiscalYear = fiscalYear <= 0 ? selectedFiscalYear : fiscalYear;

        if (string.Equals(selectedEnterprise, normalizedEnterprise, StringComparison.Ordinal) &&
            selectedFiscalYear == normalizedFiscalYear)
        {
            return false;
        }

        selectedEnterprise = normalizedEnterprise;
        selectedFiscalYear = normalizedFiscalYear;
        return true;
    }

    private static bool SetStringWithoutNotify(ref string field, string? value, string fallback)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        if (string.Equals(field, normalized, StringComparison.Ordinal))
        {
            return false;
        }

        field = normalized;
        return true;
    }

    private static bool SetDecimalWithoutNotify(ref decimal field, decimal? value, decimal fallback)
    {
        var normalized = value ?? fallback;
        if (field == normalized)
        {
            return false;
        }

        field = normalized;
        return true;
    }

    private bool SetScenarioItemsWithoutNotify(IReadOnlyList<WorkspaceScenarioItemData>? items)
    {
        if (items is null || items.Count == 0)
        {
            return false;
        }

        scenarioItems.Clear();
        foreach (var item in items)
        {
            scenarioItems.Add(new ScenarioItem(item.Id, item.Name, item.Cost));
        }

        return true;
    }

    private bool SetCustomerRowsWithoutNotify(IReadOnlyList<CustomerRow>? rows)
    {
        if (rows is null || rows.Count == 0)
        {
            return false;
        }

        if (customerRows.Count == rows.Count && customerRows.SequenceEqual(rows))
        {
            return false;
        }

        customerRows.Clear();
        foreach (var row in rows)
        {
            customerRows.Add(new CustomerRow(row.Name, row.Service, row.CityLimits));
        }

        return true;
    }

    private bool SetProjectionRowsWithoutNotify(IReadOnlyList<ProjectionRow>? rows)
    {
        if (rows is null || rows.Count == 0)
        {
            return false;
        }

        if (projectionRows.Count == rows.Count && projectionRows.SequenceEqual(rows))
        {
            return false;
        }

        projectionRows.Clear();
        foreach (var row in rows)
        {
            projectionRows.Add(new ProjectionRow(row.Year, row.Rate));
        }

        return true;
    }

    private static bool SetStringListWithoutNotify(List<string> target, IReadOnlyList<string>? source, IReadOnlyList<string> fallback)
    {
        var normalized = (source is { Count: > 0 } ? source : fallback)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (target.SequenceEqual(normalized, StringComparer.Ordinal))
        {
            return false;
        }

        target.Clear();
        target.AddRange(normalized);
        return true;
    }

    private static bool SetIntListWithoutNotify(List<int> target, IReadOnlyList<int>? source, IReadOnlyList<int> fallback)
    {
        var normalized = (source is { Count: > 0 } ? source : fallback)
            .Where(item => item > 0)
            .Distinct()
            .OrderBy(item => item);

        if (target.SequenceEqual(normalized))
        {
            return false;
        }

        target.Clear();
        target.AddRange(normalized);
        return true;
    }

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