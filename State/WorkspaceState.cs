namespace WileyCoWeb.State;

public sealed class WorkspaceState
{
    private string selectedEnterprise = "Water";
    private int selectedFiscalYear = 2026;
    private string activeScenarioName = "Base Planning Scenario";
    private string customerSearchTerm = string.Empty;
    private string selectedCustomerService = "All Services";
    private string selectedCustomerCityLimits = "All";
    private decimal currentRate = 28.50m;
    private decimal totalCosts = 412500m;
    private decimal projectedVolume = 14500m;
    private readonly List<ScenarioItem> scenarioItems = [];
    private readonly List<CustomerRow> customerRows = [];
    private readonly List<ProjectionRow> projectionRows = [];

    public event Action? Changed;

    public IReadOnlyList<string> EnterpriseOptions { get; } = ["Water", "Sewer", "Trash", "Apartments"];

    public IReadOnlyList<int> FiscalYearOptions { get; } = [2025, 2026, 2027];

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

    public IReadOnlyList<string> CustomerServiceOptions =>
    [
        "All Services",
        .. customerRows.Select(customer => customer.Service).Distinct(StringComparer.Ordinal).OrderBy(service => service)
    ];

    public IReadOnlyList<string> CustomerCityLimitOptions { get; } = ["All", "Yes", "No"];

    public string CustomerSearchTerm
    {
        get => customerSearchTerm;
        set => SetCustomerSearchTerm(value);
    }

    public string SelectedCustomerService
    {
        get => selectedCustomerService;
        set => SetCustomerServiceFilter(value);
    }

    public string SelectedCustomerCityLimits
    {
        get => selectedCustomerCityLimits;
        set => SetCustomerCityLimitsFilter(value);
    }

    public IReadOnlyList<CustomerRow> FilteredCustomers => Customers.Where(customer =>
        (string.IsNullOrWhiteSpace(customerSearchTerm) ||
         customer.Name.Contains(customerSearchTerm, StringComparison.OrdinalIgnoreCase) ||
         customer.Service.Contains(customerSearchTerm, StringComparison.OrdinalIgnoreCase) ||
         customer.CityLimits.Contains(customerSearchTerm, StringComparison.OrdinalIgnoreCase)) &&
        (string.Equals(selectedCustomerService, "All Services", StringComparison.Ordinal) ||
         string.Equals(customer.Service, selectedCustomerService, StringComparison.Ordinal)) &&
        (string.Equals(selectedCustomerCityLimits, "All", StringComparison.Ordinal) ||
         string.Equals(customer.CityLimits, selectedCustomerCityLimits, StringComparison.Ordinal)))
        .ToList();

    public int FilteredCustomerCount => FilteredCustomers.Count;

    public IReadOnlyList<ProjectionRow> ProjectionSeries => projectionRows;

    public void ApplyBootstrap(WorkspaceBootstrapData bootstrapData)
    {
        ArgumentNullException.ThrowIfNull(bootstrapData);

        var hasChanged = false;

        hasChanged |= SetSelectionWithoutNotify(bootstrapData.SelectedEnterprise, bootstrapData.SelectedFiscalYear);
        hasChanged |= SetStringWithoutNotify(ref activeScenarioName, bootstrapData.ActiveScenarioName, "Base Planning Scenario");
        hasChanged |= SetDecimalWithoutNotify(ref currentRate, bootstrapData.CurrentRate, 28.50m);
        hasChanged |= SetDecimalWithoutNotify(ref totalCosts, bootstrapData.TotalCosts, 412500m);
        hasChanged |= SetDecimalWithoutNotify(ref projectedVolume, bootstrapData.ProjectedVolume, 14500m);
        hasChanged |= SetScenarioItemsWithoutNotify(bootstrapData.ScenarioItems);
        hasChanged |= SetCustomerRowsWithoutNotify(bootstrapData.CustomerRows);
        hasChanged |= SetProjectionRowsWithoutNotify(bootstrapData.ProjectionRows);

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
        DateTime.UtcNow.ToString("O"))
    {
        ScenarioItems = scenarioItems.Select(item => new WorkspaceScenarioItemData(item.Id, item.Name, item.Cost)).ToList(),
        CustomerRows = customerRows.ToList(),
        ProjectionRows = projectionRows.ToList()
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
        var normalizedService = string.IsNullOrWhiteSpace(service) ? "All Services" : service.Trim();
        if (string.Equals(selectedCustomerService, normalizedService, StringComparison.Ordinal))
        {
            return;
        }

        selectedCustomerService = normalizedService;
        NotifyChanged();
    }

    public void SetCustomerCityLimitsFilter(string? cityLimits)
    {
        var normalizedCityLimits = string.IsNullOrWhiteSpace(cityLimits) ? "All" : cityLimits.Trim();
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

        if (!string.Equals(selectedCustomerService, "All Services", StringComparison.Ordinal))
        {
            selectedCustomerService = "All Services";
            hasChanged = true;
        }

        if (!string.Equals(selectedCustomerCityLimits, "All", StringComparison.Ordinal))
        {
            selectedCustomerCityLimits = "All";
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

    private void NotifyChanged() => Changed?.Invoke();
}

public sealed record WorkspaceBootstrapData(
    string SelectedEnterprise,
    int SelectedFiscalYear,
    string ActiveScenarioName,
    decimal? CurrentRate,
    decimal? TotalCosts,
    decimal? ProjectedVolume,
    string? LastUpdatedUtc)
{
    public List<CustomerRow>? CustomerRows { get; init; }
    public List<ProjectionRow>? ProjectionRows { get; init; }
    public List<WorkspaceScenarioItemData>? ScenarioItems { get; init; }
}

public sealed record RateComparisonPoint(string Label, double Value);

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

public sealed record WorkspaceScenarioItemData(Guid Id, string Name, decimal Cost);

public sealed record CustomerRow(string Name, string Service, string CityLimits);

public sealed record ProjectionRow(string Year, decimal Rate);