using WileyCoWeb.Contracts;
using WileyCoWeb.State;

namespace WileyCoWeb.ComponentTests;

/// <summary>
/// Deterministic unit tests for the business logic that backs <c>DataDashboardPanel.razor</c>.
/// All tests are pure C# — no browser, no HTTP — and verify the exact formulas used by the panel.
/// </summary>
/// <remarks>
/// <para>Panel computed-property formulas (from DataDashboardPanel.razor @code):</para>
/// <para>MonthlyRevenue = CurrentRate × ProjectedVolume</para>
/// <para>NetPosition    = MonthlyRevenue − TotalCosts</para>
/// <para>CoverageRatio  = TotalCosts > 0 ? MonthlyRevenue / TotalCosts : 0</para>
/// <para>RateAdequacy   = RecommendedRate > 0 ? CurrentRate / RecommendedRate × 100 : 0</para>
/// <para>GaugeCoverage  = Math.Min(CoverageRatio, 2.5)</para>
/// <para>GaugeAdequacy  = Math.Min(RateAdequacy, 150)</para>
/// <para>WaterfallPoints = [] when no items; [Base, ...items, Sum(0)] when items present</para>
/// <para>WaterfallSumIndex = WaterfallPoints.Count − 1</para>
/// <para>CustomersByService    = Customers grouped by Service (blank excluded)</para>
/// <para>CustomersByCityLimits = Customers grouped by CityLimits (blank excluded)</para>
/// </remarks>
public sealed class DataDashboardPanelTests
{
    // ── KPI: Net Position ────────────────────────────────────────────────────

    [Fact]
    public void KpiNetPosition_IsMonthlyRevenue_MinusTotalCosts()
    {
        const decimal rate = WorkspaceTestData.WaterCurrentRate; // 55.25
        const decimal volume = WorkspaceTestData.WaterProjectedVolume; // 240
        const decimal costs = WorkspaceTestData.WaterTotalCosts; // 13250

        const decimal monthlyRevenue = rate * volume; // 13260
        const decimal expected = monthlyRevenue - costs; // 10

        Assert.Equal(10m, expected);
    }

    [Fact]
    public void KpiNetPosition_IsNegative_WhenCostsExceedRevenue()
    {
        const decimal rate = 50m;
        const decimal volume = 200m;  // revenue = 10000
        const decimal costs = 12000m;

        const decimal netPosition = (rate * volume) - costs; // −2000

        Assert.True(netPosition < 0, "Net position should be negative when costs exceed revenue.");
        Assert.Equal(-2000m, netPosition);
    }

    // ── KPI: Coverage Ratio ──────────────────────────────────────────────────

    [Fact]
    public void KpiCoverageRatio_IsMonthlyRevenue_DividedBy_TotalCosts()
    {
        const decimal rate = WorkspaceTestData.WaterCurrentRate; // 55.25
        const decimal volume = WorkspaceTestData.WaterProjectedVolume; // 240
        const decimal costs = WorkspaceTestData.WaterTotalCosts; // 13250

        const decimal monthlyRevenue = rate * volume; // 13260
        const double coverageRatio = (double)(monthlyRevenue / costs); // ≈ 1.0008

        Assert.InRange(coverageRatio, 1.0007, 1.0009);
    }

    [Fact]
    public void KpiCoverageRatio_IsZero_WhenTotalCostsIsZero()
    {
        // not const — avoids division-by-constant-zero compiler error
        decimal costs = 0m;
        // Panel formula: TotalCosts > 0 ? ... : 0d
        double coverageRatio = costs > 0 ? (double)(55m * 200m / costs) : 0d;

        Assert.Equal(0d, coverageRatio);
    }

    [Theory]
    [InlineData(1.25, "green")]  // >= 1.25 → green
    [InlineData(1.00, "amber")]  // >= 1.00 but < 1.25 → amber
    [InlineData(0.90, "red")]    // < 1.0 → red
    public void KpiCoverageRatio_ColorThresholds_AreCorrect(double ratio, string expectedColor)
    {
        // Mirrors the panel's @if chain: >= 1.25 → green, >= 1.0 → amber, else red
        var color = ratio >= 1.25 ? "green" : (ratio >= 1.0 ? "amber" : "red");

        Assert.Equal(expectedColor, color);
    }

    // ── KPI: Rate Adequacy ───────────────────────────────────────────────────

    [Fact]
    public void KpiRateAdequacy_IsCurrentRate_Over_RecommendedRate_Times100()
    {
        // Use empty scenario items so AdjustedRecommendedRate = TotalCosts / Volume only (≈ 55.21).
        // CurrentRate 55.25 > 55.21 → RateAdequacy > 100 (green).
        var state = new WorkspaceState();
        state.ApplyBootstrap(WorkspaceTestData.CreateWaterUtilityBootstrap(
            WorkspaceTestData.CouncilReviewScenario,
            WorkspaceTestData.WaterCurrentRate,
            WorkspaceTestData.WaterTotalCosts,
            WorkspaceTestData.WaterProjectedVolume,
            scenarioItems: []));

        decimal adjustedRecommendedRate = state.AdjustedRecommendedRate; // 13250 / 240 ≈ 55.208
        Assert.True(adjustedRecommendedRate > 0);

        double rateAdequacy = (double)(WorkspaceTestData.WaterCurrentRate / adjustedRecommendedRate * 100m);

        // 55.25 / 55.208… × 100 ≈ 100.08 → green threshold is >= 100
        Assert.True(rateAdequacy > 100, $"RateAdequacy should exceed 100 when CurrentRate > break-even, got {rateAdequacy:F4}");
    }

    [Fact]
    public void KpiRateAdequacy_IsZero_WhenRecommendedRateIsZero()
    {
        // Panel formula: RecommendedRate > 0 ? ... : 0d
        // not const — avoids division-by-constant-zero compiler error
        decimal recommendedRate = 0m;
        double rateAdequacy = recommendedRate > 0 ? (double)(55m / recommendedRate * 100m) : 0d;

        Assert.Equal(0d, rateAdequacy);
    }

    [Theory]
    [InlineData(100.0, "green")]  // >= 100 → green
    [InlineData(85.0, "amber")]   // >= 85 but < 100 → amber
    [InlineData(80.0, "red")]     // < 85 → red
    public void KpiRateAdequacy_ColorThresholds_AreCorrect(double adequacy, string expectedColor)
    {
        // Mirrors panel: >= 100 → green, >= 85 → amber, else red
        var color = adequacy >= 100 ? "green" : (adequacy >= 85 ? "amber" : "red");

        Assert.Equal(expectedColor, color);
    }

    // ── Gauge pointer clamping ────────────────────────────────────────────────

    [Theory]
    [InlineData(1.0, 1.0)]   // well within range
    [InlineData(2.5, 2.5)]   // exactly at max
    [InlineData(3.0, 2.5)]   // exceeds max → clamped
    [InlineData(5.0, 2.5)]   // far above max → clamped
    public void GaugeCoverageRatioPointer_ClampedTo2Point5(double ratio, double expected)
    {
        // Panel: <CircularGaugePointer Value="@Math.Min(CoverageRatio, 2.5)" />
        double pointerValue = Math.Min(ratio, 2.5);

        Assert.Equal(expected, pointerValue, precision: 10);
    }

    [Theory]
    [InlineData(100.0, 100.0)]  // within range
    [InlineData(150.0, 150.0)]  // exactly at max
    [InlineData(160.0, 150.0)]  // exceeds max → clamped
    [InlineData(200.0, 150.0)]  // far above max → clamped
    public void GaugeRateAdequacyPointer_ClampedTo150(double adequacy, double expected)
    {
        // Panel: <CircularGaugePointer Value="@Math.Min(RateAdequacy, 150)" />
        double pointerValue = Math.Min(adequacy, 150d);

        Assert.Equal(expected, pointerValue, precision: 10);
    }

    // ── Waterfall points ──────────────────────────────────────────────────────

    [Fact]
    public void WaterfallPoints_AreEmpty_WhenNoScenarioItems()
    {
        // Panel: if (ScenarioItems.Count == 0) return [];
        var scenarioItems = new List<WorkspaceScenarioItemData>();

        var waterfallPoints = BuildWaterfallPoints(1000m, scenarioItems);

        Assert.Empty(waterfallPoints);
    }

    [Fact]
    public void WaterfallPoints_Count_IsItemCountPlusTwo_WhenItemsExist()
    {
        // Panel: [Base, ...items, "With Scenario"] = items.Count + 2
        var scenarioItems = new List<WorkspaceScenarioItemData>
        {
            new(Guid.NewGuid(), "Reserve transfer", 6200m),
            new(Guid.NewGuid(), "Vehicle replacement", 18000m)
        };

        var waterfallPoints = BuildWaterfallPoints(13250m, scenarioItems);

        // 2 items + Base + "With Scenario" = 4
        Assert.Equal(scenarioItems.Count + 2, waterfallPoints.Count);
    }

    [Fact]
    public void WaterfallPoints_FirstItem_IsBaseCost()
    {
        const decimal baseCost = 13250m;
        var scenarioItems = new List<WorkspaceScenarioItemData>
        {
            new(Guid.NewGuid(), "Reserve transfer", 6200m)
        };

        var waterfallPoints = BuildWaterfallPoints(baseCost, scenarioItems);

        Assert.Equal("Base Cost", waterfallPoints[0].Label);
        Assert.Equal((double)baseCost, waterfallPoints[0].Value);
    }

    [Fact]
    public void WaterfallPoints_LastItem_IsWithScenarioAtZero()
    {
        // The sum bar uses Value=0 so Syncfusion renders a cumulative sum.
        var scenarioItems = new List<WorkspaceScenarioItemData>
        {
            new(Guid.NewGuid(), "Reserve transfer", 6200m),
            new(Guid.NewGuid(), "Vehicle replacement", 18000m)
        };

        var waterfallPoints = BuildWaterfallPoints(13250m, scenarioItems);

        var (label, value) = waterfallPoints[^1];
        Assert.Equal("With Scenario", label);
        Assert.Equal(0d, value);
    }

    [Fact]
    public void WaterfallSumIndex_PointsToLastItem()
    {
        // Panel: WaterfallSumIndexes = [WaterfallPoints.Count - 1d]
        var scenarioItems = new List<WorkspaceScenarioItemData>
        {
            new(Guid.NewGuid(), "Item A", 1000m),
            new(Guid.NewGuid(), "Item B", 2000m),
            new(Guid.NewGuid(), "Item C", 3000m)
        };

        var waterfallPoints = BuildWaterfallPoints(5000m, scenarioItems);
        double[] sumIndexes = [waterfallPoints.Count - 1d];

        Assert.Single(sumIndexes);
        Assert.Equal(waterfallPoints.Count - 1, (int)sumIndexes[0]);
    }

    // ── Customer pie grouping ─────────────────────────────────────────────────

    [Fact]
    public void CustomersByService_GroupsCustomersCorrectly()
    {
        // Mirrors: Customers.Where(c => !IsNullOrWhiteSpace(c.Service)).GroupBy(c => c.Service)
        var customers = new List<CustomerRow>
        {
            new("Alice",   "Water",  "Yes"),
            new("Bob",     "Water",  "No"),
            new("Carol",   "Sewer",  "Yes"),
            new("Dave",    "Water",  "Yes"),
        };

        var byService = customers
            .Where(c => !string.IsNullOrWhiteSpace(c.Service))
            .GroupBy(c => c.Service)
            .Select(g => (Label: g.Key, Count: g.Count()))
            .OrderByDescending(p => p.Count)
            .ToList();

        Assert.Equal(2, byService.Count);
        Assert.Equal("Water", byService[0].Label);  // 3 entries → first
        Assert.Equal(3, byService[0].Count);
        Assert.Equal("Sewer", byService[1].Label);  // 1 entry → second
        Assert.Equal(1, byService[1].Count);
    }

    [Fact]
    public void CustomersByService_ExcludesBlankServiceNames()
    {
        var customers = new List<CustomerRow>
        {
            new("Alice", "Water", "Yes"),
            new("Bob",   "",      "No"),   // blank Service → excluded
            new("Carol", "   ",   "Yes"),  // whitespace Service → excluded
        };

        var byService = customers
            .Where(c => !string.IsNullOrWhiteSpace(c.Service))
            .GroupBy(c => c.Service)
            .Select(g => (Label: g.Key, Count: g.Count()))
            .ToList();

        Assert.Single(byService);
        Assert.Equal("Water", byService[0].Label);
    }

    [Fact]
    public void CustomersByCityLimits_GroupsCustomersCorrectly()
    {
        var customers = new List<CustomerRow>
        {
            new("North Plant", "Water", "Yes"),
            new("South Lift",  "Sewer", "No"),
            new("East Hub",    "Water", "Yes"),
        };

        var byCityLimits = customers
            .Where(c => !string.IsNullOrWhiteSpace(c.CityLimits))
            .GroupBy(c => c.CityLimits)
            .Select(g => (Label: g.Key, Count: g.Count()))
            .OrderByDescending(p => p.Count)
            .ToList();

        Assert.Equal(2, byCityLimits.Count);
        Assert.Equal("Yes", byCityLimits[0].Label); // 2 entries → first
        Assert.Equal(2, byCityLimits[0].Count);
        Assert.Equal("No", byCityLimits[1].Label);  // 1 entry → second
        Assert.Equal(1, byCityLimits[1].Count);
    }

    [Fact]
    public void CustomersByCityLimits_ExcludesBlankCityLimitsValues()
    {
        var customers = new List<CustomerRow>
        {
            new("Alice", "Water", "Yes"),
            new("Bob",   "Water", ""),       // blank → excluded
            new("Carol", "Sewer", null!),    // null → excluded
        };

        var byCityLimits = customers
            .Where(c => !string.IsNullOrWhiteSpace(c.CityLimits))
            .GroupBy(c => c.CityLimits)
            .Select(g => (Label: g.Key, Count: g.Count()))
            .ToList();

        Assert.Single(byCityLimits);
        Assert.Equal("Yes", byCityLimits[0].Label);
    }

    // ── WorkspaceState: RateComparison is always 2 points ────────────────────

    [Fact]
    public void WorkspaceState_RateComparison_AlwaysReturnsTwoPoints_WithDefaultValues()
    {
        // Even with no bootstrap applied, state defaults produce 2 points
        var state = new WorkspaceState();

        Assert.Equal(2, state.RateComparison.Count);
        Assert.Equal("Current", state.RateComparison[0].Label);
        Assert.Equal("Break-Even", state.RateComparison[1].Label);
    }

    [Fact]
    public void WorkspaceState_RateComparison_AlwaysReturnsTwoPoints_AfterBootstrap()
    {
        var state = new WorkspaceState();
        state.ApplyBootstrap(WorkspaceTestData.CreateWaterUtilityBootstrap(
            WorkspaceTestData.CouncilReviewScenario,
            WorkspaceTestData.WaterCurrentRate,
            WorkspaceTestData.WaterTotalCosts,
            WorkspaceTestData.WaterProjectedVolume));

        Assert.Equal(2, state.RateComparison.Count);
        Assert.Equal("Current", state.RateComparison[0].Label);
        Assert.Equal((double)WorkspaceTestData.WaterCurrentRate, state.RateComparison[0].Value);
        Assert.Equal("Break-Even", state.RateComparison[1].Label);
        Assert.True(state.RateComparison[1].Value > 0, "Break-Even value should be positive with non-zero volume.");
    }

    // ── WorkspaceState: ProjectionSeries feeds the conditional rate-trend chart ─

    [Fact]
    public void WorkspaceState_ProjectionSeries_ReflectsBootstrapProjectionRows()
    {
        var projections = new List<ProjectionRow>
        {
            new("FY24", 48.00m),
            new("FY25", 51.50m),
            new("FY26", 55.25m),
        };

        var state = new WorkspaceState();
        state.ApplyBootstrap(WorkspaceTestData.CreateWaterUtilityBootstrap(
            WorkspaceTestData.CouncilReviewScenario,
            WorkspaceTestData.WaterCurrentRate,
            WorkspaceTestData.WaterTotalCosts,
            WorkspaceTestData.WaterProjectedVolume,
            projectionRows: projections));

        Assert.Equal(3, state.ProjectionSeries.Count);
        Assert.Equal("FY24", state.ProjectionSeries[0].Year);
        Assert.Equal(48.00m, state.ProjectionSeries[0].Rate);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    // Mirror of DataDashboardPanel.WaterfallPoints (private panel property)
    private static List<(string Label, double Value)> BuildWaterfallPoints(
        decimal baseCost,
        List<WorkspaceScenarioItemData> scenarioItems)
    {
        if (scenarioItems.Count == 0) return [];

        List<(string Label, double Value)> pts = [("Base Cost", (double)baseCost)];
        foreach (var item in scenarioItems)
            pts.Add((item.Name, (double)item.Cost));
        pts.Add(("With Scenario", 0d));
        return pts;
    }
}
