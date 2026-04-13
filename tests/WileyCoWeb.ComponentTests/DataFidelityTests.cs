using WileyCoWeb.Contracts;
using WileyCoWeb.State;

namespace WileyCoWeb.ComponentTests;

/// <summary>
/// Data fidelity tests: verify that field values declared in a <see cref="WorkspaceBootstrapData"/>
/// survive every transformation in the DB → DTO → <see cref="WorkspaceState"/> → UI-property chain
/// without numeric drift, field aliasing, or silent truncation.
/// These tests are pure in-process — no HTTP or EF Core — so they run in every CI tier.
/// </summary>
public sealed class DataFidelityTests
{
    // ─── Round-trip: WorkspaceBootstrapData → WorkspaceState → ToBootstrapData ──

    [Fact]
    public void BreakEvenPanel_CoreDecimalFields_RoundTripThroughWorkspaceState_WithoutDrift()
    {
        const decimal currentRate = 72.33m;
        const decimal totalCosts = 18_450.75m;
        const decimal projectedVolume = 312m;

        var state = new WorkspaceState();
        state.ApplyBootstrap(WorkspaceTestData.CreateWaterUtilityBootstrap(
            WorkspaceTestData.CouncilReviewScenario,
            currentRate,
            totalCosts,
            projectedVolume));

        var roundTripped = state.ToBootstrapData();

        Assert.Equal(currentRate, roundTripped.CurrentRate);
        Assert.Equal(totalCosts, roundTripped.TotalCosts);
        Assert.Equal(projectedVolume, roundTripped.ProjectedVolume);
    }

    [Fact]
    public void RatesPanel_ProjectionRows_PreserveYearLabelsAndRatesAfterApplyBootstrap()
    {
        var projections = new List<ProjectionRow>
        {
            new("FY24", 48.00m),
            new("FY25", 51.50m),
            new("FY26", 55.25m),
            new("FY27", 59.75m)
        };

        var state = new WorkspaceState();
        state.ApplyBootstrap(WorkspaceTestData.CreateWaterUtilityBootstrap(
            WorkspaceTestData.CouncilReviewScenario,
            WorkspaceTestData.WaterCurrentRate,
            WorkspaceTestData.WaterTotalCosts,
            WorkspaceTestData.WaterProjectedVolume,
            projectionRows: projections));

        var roundTripped = state.ToBootstrapData();

        Assert.NotNull(roundTripped.ProjectionRows);
        Assert.Equal(projections.Count, roundTripped.ProjectionRows.Count);
        for (int i = 0; i < projections.Count; i++)
        {
            Assert.Equal(projections[i].Year, roundTripped.ProjectionRows[i].Year);
            Assert.Equal(projections[i].Rate, roundTripped.ProjectionRows[i].Rate);
        }
    }

    [Fact]
    public void ScenarioPlannerPanel_ScenarioItems_PreserveIdNameAndCostAfterRoundTrip()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var scenarios = new List<WorkspaceScenarioItemData>
        {
            new(id1, "Reserve transfer", 6_200m),
            new(id2, "Vehicle replacement", 18_000m)
        };

        var state = new WorkspaceState();
        state.ApplyBootstrap(WorkspaceTestData.CreateWaterUtilityBootstrap(
            WorkspaceTestData.CouncilReviewScenario,
            WorkspaceTestData.WaterCurrentRate,
            WorkspaceTestData.WaterTotalCosts,
            WorkspaceTestData.WaterProjectedVolume,
            scenarioItems: scenarios));

        var roundTripped = state.ToBootstrapData();

        Assert.NotNull(roundTripped.ScenarioItems);
        Assert.Equal(2, roundTripped.ScenarioItems.Count);

        var rt1 = roundTripped.ScenarioItems.Single(s => s.Id == id1);
        Assert.Equal("Reserve transfer", rt1.Name);
        Assert.Equal(6_200m, rt1.Cost);

        var rt2 = roundTripped.ScenarioItems.Single(s => s.Id == id2);
        Assert.Equal("Vehicle replacement", rt2.Name);
        Assert.Equal(18_000m, rt2.Cost);
    }

    [Fact]
    public void CustomerViewerPanel_CustomerRows_PreserveNameServiceAndCityLimitsAfterRoundTrip()
    {
        var customers = new List<CustomerRow>
        {
            new("North Plant", "Water", "Yes"),
            new("South Lift", "Sewer", "No"),
            new("East Station", "Water", "Yes")
        };

        var state = new WorkspaceState();
        state.ApplyBootstrap(WorkspaceTestData.CreateWaterUtilityBootstrap(
            WorkspaceTestData.CouncilReviewScenario,
            WorkspaceTestData.WaterCurrentRate,
            WorkspaceTestData.WaterTotalCosts,
            WorkspaceTestData.WaterProjectedVolume,
            customerRows: customers));

        var roundTripped = state.ToBootstrapData();

        Assert.NotNull(roundTripped.CustomerRows);
        Assert.Equal(3, roundTripped.CustomerRows.Count);
        Assert.Contains(roundTripped.CustomerRows, r => r.Name == "North Plant" && r.Service == "Water" && r.CityLimits == "Yes");
        Assert.Contains(roundTripped.CustomerRows, r => r.Name == "South Lift" && r.Service == "Sewer" && r.CityLimits == "No");
        Assert.Contains(roundTripped.CustomerRows, r => r.Name == "East Station" && r.Service == "Water" && r.CityLimits == "Yes");
    }

    [Fact]
    public void TrendsPanel_ProjectionRows_MaintainChronologicalIntegrity()
    {
        var projections = new List<ProjectionRow>
        {
            new("FY24", 48.00m),
            new("FY25", 51.50m),
            new("FY26", 55.25m),
            new("FY27", 59.75m)
        };

        var state = new WorkspaceState();
        state.ApplyBootstrap(WorkspaceTestData.CreateWaterUtilityBootstrap(
            WorkspaceTestData.CouncilReviewScenario,
            WorkspaceTestData.WaterCurrentRate,
            WorkspaceTestData.WaterTotalCosts,
            WorkspaceTestData.WaterProjectedVolume,
            projectionRows: projections));

        var roundTripped = state.ToBootstrapData();

        Assert.NotNull(roundTripped.ProjectionRows);
        var rates = roundTripped.ProjectionRows.Select(r => r.Rate).ToList();
        for (int i = 1; i < rates.Count; i++)
        {
            Assert.True(rates[i] >= rates[i - 1],
                $"Rate at index {i} ({rates[i]}) regressed below prior ({rates[i - 1]}) — projection data mutated during state round-trip.");
        }
    }

    [Fact]
    public void DecisionSupportPanel_EnterpriseOptionsAndScenarioName_PreservedAfterRoundTrip()
    {
        var enterpriseOptions = new List<string> { "Water Utility", "Sanitation Utility", "Storm Water" };

        var state = new WorkspaceState();
        state.ApplyBootstrap(WorkspaceTestData.CreateWaterUtilityBootstrap(
            WorkspaceTestData.CouncilReviewScenario,
            WorkspaceTestData.WaterCurrentRate,
            WorkspaceTestData.WaterTotalCosts,
            WorkspaceTestData.WaterProjectedVolume,
            enterpriseOptions: enterpriseOptions));

        var roundTripped = state.ToBootstrapData();

        Assert.NotNull(roundTripped.EnterpriseOptions);
        Assert.Equal(enterpriseOptions.Count, roundTripped.EnterpriseOptions.Count);
        Assert.Equal(enterpriseOptions, roundTripped.EnterpriseOptions);
        Assert.Equal(WorkspaceTestData.CouncilReviewScenario, roundTripped.ActiveScenarioName);
    }

    // ─── Extreme / boundary values ───────────────────────────────────────────────

    [Fact]
    public void AllPanels_ZeroRate_IsPreservedWithoutSubstitution()
    {
        // A rate that becomes zero during a scenario (e.g. rate holiday) must not
        // be silently promoted to null or a default value.
        var state = new WorkspaceState();
        state.ApplyBootstrap(WorkspaceTestData.CreateWaterUtilityBootstrap(
            WorkspaceTestData.CouncilReviewScenario,
            currentRate: 0m,
            totalCosts: 0m,
            projectedVolume: 0m));

        var roundTripped = state.ToBootstrapData();

        Assert.Equal(0m, roundTripped.CurrentRate);
        Assert.Equal(0m, roundTripped.TotalCosts);
        Assert.Equal(0m, roundTripped.ProjectedVolume);
    }

    [Fact]
    public void AllPanels_LargeDecimalValues_DoNotLosePrecision()
    {
        const decimal rate = 9_999.9999m;
        const decimal costs = 99_999_999.99m;
        const decimal volume = 1_000_000m;

        var state = new WorkspaceState();
        state.ApplyBootstrap(WorkspaceTestData.CreateWaterUtilityBootstrap(
            WorkspaceTestData.CouncilReviewScenario,
            currentRate: rate,
            totalCosts: costs,
            projectedVolume: volume));

        var roundTripped = state.ToBootstrapData();

        Assert.Equal(rate, roundTripped.CurrentRate);
        Assert.Equal(costs, roundTripped.TotalCosts);
        Assert.Equal(volume, roundTripped.ProjectedVolume);
    }

    [Fact]
    public void ScenarioPlannerPanel_EmptyScenarioList_RoundTripsDontThrow()
    {
        var state = new WorkspaceState();
        state.ApplyBootstrap(WorkspaceTestData.CreateWaterUtilityBootstrap(
            WorkspaceTestData.CouncilReviewScenario,
            WorkspaceTestData.WaterCurrentRate,
            WorkspaceTestData.WaterTotalCosts,
            WorkspaceTestData.WaterProjectedVolume,
            scenarioItems: []));

        var roundTripped = state.ToBootstrapData();

        // ScenarioItems should be non-null (may be empty or null — either is valid)
        // but the operation must not throw.
        Assert.True(roundTripped.ScenarioItems is null || roundTripped.ScenarioItems.Count == 0,
            "Empty scenario list must round-trip as null or empty, not as an error or phantom item.");
    }

    [Fact]
    public void CustomerViewerPanel_EmptyCustomerList_RoundTripsDontThrow()
    {
        var state = new WorkspaceState();
        state.ApplyBootstrap(WorkspaceTestData.CreateWaterUtilityBootstrap(
            WorkspaceTestData.CouncilReviewScenario,
            WorkspaceTestData.WaterCurrentRate,
            WorkspaceTestData.WaterTotalCosts,
            WorkspaceTestData.WaterProjectedVolume,
            customerRows: []));

        var roundTripped = state.ToBootstrapData();

        Assert.True(roundTripped.CustomerRows is null || roundTripped.CustomerRows.Count == 0,
            "Empty customer list must round-trip as null or empty without error.");
    }

    // ─── LastUpdatedUtc fidelity ─────────────────────────────────────────────────

    [Fact]
    public void AllPanels_LastUpdatedUtcString_IsPreservedExactly()
    {
        const string timestamp = "2026-04-13T14:30:00.0000000Z";

        var state = new WorkspaceState();
        state.ApplyBootstrap(WorkspaceTestData.CreateWaterUtilityBootstrap(
            WorkspaceTestData.CouncilReviewScenario,
            WorkspaceTestData.WaterCurrentRate,
            WorkspaceTestData.WaterTotalCosts,
            WorkspaceTestData.WaterProjectedVolume,
            lastUpdatedUtc: timestamp));

        var roundTripped = state.ToBootstrapData();

        Assert.Equal(timestamp, roundTripped.LastUpdatedUtc);
    }

    // ─── Enterprise / year fidelity ─────────────────────────────────────────────

    [Fact]
    public void AllPanels_SelectedEnterpriseAndFiscalYear_ArePreservedAfterApplyBootstrap()
    {
        var state = new WorkspaceState();
        state.ApplyBootstrap(WorkspaceTestData.CreateWaterUtilityBootstrap(
            WorkspaceTestData.CouncilReviewScenario,
            WorkspaceTestData.WaterCurrentRate,
            WorkspaceTestData.WaterTotalCosts,
            WorkspaceTestData.WaterProjectedVolume));

        var roundTripped = state.ToBootstrapData();

        Assert.Equal(WorkspaceTestData.WaterUtility, roundTripped.SelectedEnterprise);
        Assert.Equal(WorkspaceTestData.WaterFiscalYear, roundTripped.SelectedFiscalYear);
    }
}
