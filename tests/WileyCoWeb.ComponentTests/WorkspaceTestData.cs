using WileyCoWeb.Contracts;
using WileyCoWeb.State;

namespace WileyCoWeb.ComponentTests;

internal static class WorkspaceTestData
{
    public const string WaterUtility = "Water Utility";
    public const string SanitationUtility = "Wiley Sanitation District";
    public const string TrashUtility = "Trash";
    public const string Apartments = "Apartments";
    public const string ArchivedUtility = "Archived Utility";
    public const string CouncilReviewScenario = "Council Review Scenario";
    public const string CouncilReview = "Council Review";
    public const string BasePlanningScenario = "Base Planning Scenario";
    public const string LiveApiScenario = "Live API Scenario";
    public const string TargetedApiScenario = "Targeted API snapshot";
    public const string ApiSnapshotScenario = "API snapshot";
    public const string BrowserRestoredScenario = "Browser Restored Scenario";
    public const string PersistedScenario = "Persisted scenario";
    public const string WaterPlanningSnapshot = "Water Utility planning snapshot";
    public const string WaterFY2026RateSnapshot = "Water FY2026 rate snapshot";
    public const int WaterFiscalYear = 2026;
    public const int PriorFiscalYear = 2025;
    public const decimal WaterCurrentRate = 55.25m;
    public const decimal WaterTotalCosts = 13250m;
    public const decimal WaterProjectedVolume = 240m;
    public const decimal ApiCurrentRate = 31.25m;
    public const decimal ApiTotalCosts = 98000m;
    public const decimal ApiProjectedVolume = 4500m;
    public const decimal BaselineCurrentRate = 61.75m;
    public const decimal BaselineTotalCosts = 15500m;
    public const decimal BaselineProjectedVolume = 275m;
    public const string SavedBaselineMessage = "Saved baseline values for Water Utility FY 2026.";
    public const string QuickBooksAssistantContextSummary = "quickbooks-ledger.csv for Water Utility FY 2026: 2 rows parsed, 0 duplicates flagged, file duplicate = False.";

    public static WorkspaceBootstrapData CreateWaterUtilityBootstrap(
        string scenarioName,
        decimal currentRate,
        decimal totalCosts,
        decimal projectedVolume,
        string? lastUpdatedUtc = null,
        IReadOnlyList<string>? enterpriseOptions = null,
        IReadOnlyList<int>? fiscalYearOptions = null,
        IReadOnlyList<WorkspaceScenarioItemData>? scenarioItems = null,
        IReadOnlyList<CustomerRow>? customerRows = null,
        IReadOnlyList<ProjectionRow>? projectionRows = null)
    {
        return new WorkspaceBootstrapData(
            WaterUtility,
            WaterFiscalYear,
            scenarioName,
            currentRate,
            totalCosts,
            projectedVolume,
            lastUpdatedUtc ?? "2026-04-05T12:00:00Z")
        {
            EnterpriseOptions = enterpriseOptions?.ToList() ?? [WaterUtility, SanitationUtility, TrashUtility, Apartments],
            FiscalYearOptions = fiscalYearOptions?.ToList() ?? [PriorFiscalYear, WaterFiscalYear],
            CustomerServiceOptions = ["All Services", "Water", "Sewer"],
            CustomerCityLimitOptions = ["All", "Yes", "No"],
            CustomerRows = customerRows?.ToList() ??
                [
                    new CustomerRow("North Plant", "Water", "Yes"),
                    new CustomerRow("South Lift", "Sewer", "No")
                ],
            ProjectionRows = projectionRows?.ToList() ??
                [
                    new ProjectionRow("FY25", 51.40m),
                    new ProjectionRow("FY26", currentRate)
                ],
            ScenarioItems = scenarioItems?.ToList() ??
                [
                    new WorkspaceScenarioItemData(Guid.Parse("b94d0f45-1f42-4b4d-93d7-6e9dbe3a1b01"), "Reserve transfer", 6200m),
                    new WorkspaceScenarioItemData(Guid.Parse("b94d0f45-1f42-4b4d-93d7-6e9dbe3a1b02"), "Vehicle replacement", 18000m)
                ]
        };
    }

    public static WorkspaceBootstrapData CreatePersistedBootstrap()
    {
        return new WorkspaceBootstrapData(
            TrashUtility,
            PriorFiscalYear,
            PersistedScenario,
            33m,
            123000m,
            5800m,
            "2026-04-05T12:00:00.0000000Z");
    }

    public static WorkspaceBootstrapData CreateBrowserRestoredBootstrap()
    {
        return new WorkspaceBootstrapData(
            SanitationUtility,
            PriorFiscalYear,
            BrowserRestoredScenario,
            47.10m,
            10125m,
            214m,
            "2026-04-05T12:00:00.0000000Z");
    }

    public static WorkspaceState CreateWaterUtilityState()
    {
        var state = new WorkspaceState();
        state.ApplyBootstrap(CreateWaterUtilityBootstrap(CouncilReviewScenario, WaterCurrentRate, WaterTotalCosts, WaterProjectedVolume));
        return state;
    }
}