using WileyCoWeb.State;

namespace WileyCoWeb.ComponentTests;

public sealed class WorkspaceStateTests
{
    [Fact]
    public void Defaults_ExposeExpectedValues()
    {
        var state = new WorkspaceState();

        Assert.Equal("Water", state.SelectedEnterprise);
        Assert.Equal(2026, state.SelectedFiscalYear);
        Assert.Equal("Base Planning Scenario", state.ActiveScenarioName);
        Assert.Equal(28.50m, state.CurrentRate);
        Assert.Equal(412500m, state.TotalCosts);
        Assert.Equal(14500m, state.ProjectedVolume);
        Assert.Equal(28.448275862068965517241379310m, state.RecommendedRate);
        Assert.Equal(0.051724137931034482758620690m, state.RateDelta);
        Assert.Equal("Water FY 2026 | Base Planning Scenario", state.ContextSummary);
        Assert.Collection(state.RateComparison,
            point => Assert.Equal("Current", point.Label),
            point => Assert.Equal("Break-Even", point.Label));
    }

    [Fact]
    public void Mutations_NormalizeValues_AndRaiseChanged()
    {
        var state = new WorkspaceState();
        var changedCount = 0;
        state.Changed += () => changedCount++;

        state.SetSelection("  Sewer  ", 2027);
        state.SetActiveScenarioName("  FY 2027 Reforecast  ");
        state.SetCurrentRate(31.25m);
        state.SetTotalCosts(500000m);
        state.SetProjectedVolume(16000m);
        state.SetCustomerSearchTerm("  Wiley  ");
        state.SetCustomerServiceFilter("  Trash  ");
        state.SetCustomerCityLimitsFilter("  Yes  ");
        state.AddScenarioItem("  Truck Replacement  ", 2500m);

        var scenarioItemId = state.ScenarioItems.Single().Id;
        state.UpdateScenarioItem(scenarioItemId, "  Fleet Replacement  ", 3000m);
        state.RemoveScenarioItem(scenarioItemId);
        state.ClearCustomerFilters();
        state.Refresh();

        Assert.Equal("Sewer", state.SelectedEnterprise);
        Assert.Equal(2027, state.SelectedFiscalYear);
        Assert.Equal("FY 2027 Reforecast", state.ActiveScenarioName);
        Assert.Equal(31.25m, state.CurrentRate);
        Assert.Equal(500000m, state.TotalCosts);
        Assert.Equal(16000m, state.ProjectedVolume);
        Assert.Equal(string.Empty, state.CustomerSearchTerm);
        Assert.Equal("All Services", state.SelectedCustomerService);
        Assert.Equal("All", state.SelectedCustomerCityLimits);
        Assert.Empty(state.ScenarioItems);
        Assert.True(changedCount >= 12);
    }

    [Fact]
    public void ApplyBootstrap_AndToBootstrapData_PreserveCollections()
    {
        var state = new WorkspaceState();
        var changedCount = 0;
        state.Changed += () => changedCount++;

        var bootstrap = new WorkspaceBootstrapData(
            "Trash",
            2025,
            "Trash planning",
            22.75m,
            132000m,
            5100m,
            "2026-04-05T12:00:00.0000000Z")
        {
            ScenarioItems = new List<WorkspaceScenarioItemData>
            {
                new(Guid.NewGuid(), "Truck Replacement", 2500m),
                new(Guid.NewGuid(), "Reserve Increase", 1200m)
            },
            CustomerRows = new List<CustomerRow>
            {
                new("Dana", "Trash", "Yes"),
                new("Bea", "Water", "No")
            },
            ProjectionRows = new List<ProjectionRow>
            {
                new("2025", 22.75m),
                new("2026", 24.10m)
            }
        };

        state.ApplyBootstrap(bootstrap);

        Assert.Equal(1, changedCount);
        Assert.Equal("Trash", state.SelectedEnterprise);
        Assert.Equal(2025, state.SelectedFiscalYear);
        Assert.Equal("Trash planning", state.ActiveScenarioName);
        Assert.Equal(22.75m, state.CurrentRate);
        Assert.Equal(132000m, state.TotalCosts);
        Assert.Equal(5100m, state.ProjectedVolume);
        Assert.Equal(2, state.ScenarioItems.Count);
        Assert.Equal(2, state.Customers.Count);
        Assert.Equal(2, state.ProjectionSeries.Count);
        Assert.Contains("Trash", state.CustomerServiceOptions);
        Assert.Contains(state.FilteredCustomers, customer => customer.Name == "Dana");

        var roundTrip = state.ToBootstrapData();

        Assert.Equal("Trash", roundTrip.SelectedEnterprise);
        Assert.Equal(2025, roundTrip.SelectedFiscalYear);
        Assert.Equal(2, roundTrip.ScenarioItems?.Count);
        Assert.Equal(2, roundTrip.CustomerRows?.Count);
        Assert.Equal(2, roundTrip.ProjectionRows?.Count);
    }
}
