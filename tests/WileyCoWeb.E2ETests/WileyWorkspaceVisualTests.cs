using Applitools;
using Applitools.Playwright;
using Applitools.Playwright.Fluent;
using Applitools.Utils.Geometry;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace WileyCoWeb.E2ETests;

/// <summary>
/// Applitools Eyes visual regression tests for Wiley Widget Syncfusion panels.
/// Tests gate on WILEYCO_E2E_BASE_URL and APPLITOOLS_API_KEY — both must be set.
/// On first run, Eyes captures baselines. Subsequent runs compare against them.
/// Results are reviewed in the Applitools Eyes dashboard; a small local summary is emitted for CI and log review.
/// </summary>
public sealed class WileyWorkspaceVisualTests : IDisposable
{
    private const int ReadyTimeoutMilliseconds = 90_000;
    private const int NavigationTimeoutMilliseconds = 30_000;
    private const int ChartSettleMilliseconds  = 15_000;

    private readonly ClassicRunner _runner = new();

    public void Dispose()
    {
        var summary = _runner.GetAllTestResults(false);
        var exportedSummary = ApplitoolsResultWriter.WriteSummary(summary, nameof(WileyWorkspaceVisualTests));
        ApplitoolsResultWriter.ReactToResults(exportedSummary);
    }

    [Fact]
    public async Task Visual_WorkspaceOverview_MatchesBaseline()
    {
        await RunVisualTestAsync("/wiley-workspace", "Workspace Overview - Hero Banner", static async (eyes, page) =>
        {
            await page.Locator("#workspace-overview-hero").WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = ChartSettleMilliseconds });

            eyes.Check("Overview hero banner",
                Target.Region(page.Locator("#workspace-overview-hero")).Fully());
            await Task.CompletedTask;
        });
    }

    [Fact]
    public async Task Visual_WorkspaceDocumentCenter_MatchesBaseline()
    {
        await RunVisualTestAsync("/wiley-workspace", "Workspace Overview - Document Center", static async (eyes, page) =>
        {
            await page.Locator("#workspace-document-center").WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = ChartSettleMilliseconds });

            eyes.Check("Document center card",
                Target.Region(page.Locator("#workspace-document-center")).Fully());
        });
    }

    [Fact]
    public async Task Visual_WorkspaceOverview_KeyTiles_MatchBaseline()
    {
        await RunVisualTestAsync("/wiley-workspace", "Workspace Overview - Key Tiles", static async (eyes, page) =>
        {
            await page.Locator("#workspace-overview-dashboard").WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = ChartSettleMilliseconds });

            eyes.Check("Overview break-even tile",
                Target.Region(page.Locator("#overview-break-even")).Fully());
            eyes.Check("Overview rates tile",
                Target.Region(page.Locator("#overview-rates")).Fully());
            eyes.Check("Overview import tile",
                Target.Region(page.Locator("#overview-import")).Fully());
            eyes.Check("Overview scenario tile",
                Target.Region(page.Locator("#overview-scenario")).Fully());
            eyes.Check("Overview customer tile",
                Target.Region(page.Locator("#overview-customers")).Fully());
            eyes.Check("Overview trends tile",
                Target.Region(page.Locator("#overview-trends")).Fully());
            eyes.Check("Overview decision support tile",
                Target.Region(page.Locator("#overview-ai")).Fully());
            eyes.Check("Overview data dashboard tile",
                Target.Region(page.Locator("#overview-data-dashboard")).Fully());
        });
    }

    [Fact]
    public async Task Visual_NavMenu_MatchesBaseline()
    {
        await RunVisualTestAsync("/wiley-workspace", "Nav Menu", static async (eyes, page) =>
        {
            eyes.Check("Sidebar navigation",
                Target.Region(page.Locator(".sidebar-nav, nav, .e-sidebar")).Fully());
            await Task.CompletedTask;
        });
    }

    [Fact]
    public async Task Visual_DataDashboard_KpiCards_MatchBaseline()
    {
        await RunVisualTestAsync("/wiley-workspace/data-dashboard", "Data Dashboard – KPI Cards", static async (eyes, page) =>
        {
            await page.Locator("#coverage-ratio-gauge").WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = ChartSettleMilliseconds });
            eyes.Check("KPI card row",
                Target.Region(page.Locator("#kpi-net-position, #kpi-coverage-ratio, #kpi-rate-adequacy, #kpi-scenario-pressure")).Fully());
        });
    }

    [Fact]
    public async Task Visual_DataDashboard_CoverageGauge_MatchesBaseline()
    {
        await RunVisualTestAsync("/wiley-workspace/data-dashboard", "Data Dashboard – Coverage Gauge", static async (eyes, page) =>
        {
            await page.Locator("#coverage-ratio-gauge").WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = ChartSettleMilliseconds });
            eyes.Check("Coverage ratio circular gauge",
                Target.Region(page.Locator("#coverage-ratio-gauge")).Fully());
        });
    }

    [Fact]
    public async Task Visual_DataDashboard_RateAdequacyGauge_MatchesBaseline()
    {
        await RunVisualTestAsync("/wiley-workspace/data-dashboard", "Data Dashboard – Rate Adequacy Gauge", static async (eyes, page) =>
        {
            await page.Locator("#rate-adequacy-gauge").WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = ChartSettleMilliseconds });
            eyes.Check("Rate adequacy circular gauge",
                Target.Region(page.Locator("#rate-adequacy-gauge")).Fully());
        });
    }

    [Fact]
    public async Task Visual_DataDashboard_RateComparisonChart_MatchesBaseline()
    {
        await RunVisualTestAsync("/wiley-workspace/data-dashboard", "Data Dashboard – Rate Comparison", static async (eyes, page) =>
        {
            await page.Locator("#budget-variance-chart").WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = ChartSettleMilliseconds });
            eyes.Check("Rate comparison bar chart",
                Target.Region(page.Locator("#rate-comparison-section")).Fully());
        });
    }

    [Fact]
    public async Task Visual_DataDashboard_FullPanel_MatchesBaseline()
    {
        await RunVisualTestAsync("/wiley-workspace/data-dashboard", "Data Dashboard - Panel Region", static async (eyes, page) =>
        {
            await page.Locator("#budget-variance-chart").WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = ChartSettleMilliseconds });
            eyes.Check("Full data dashboard panel",
                Target.Region(page.Locator("#data-dashboard-panel")).Fully());
        });
    }

    [Fact]
    public async Task Visual_BreakEvenPanel_MatchesBaseline()
    {
        await RunVisualTestAsync("/wiley-workspace/break-even", "Break-Even Panel - Sections", static async (eyes, page) =>
        {
            await page.Locator("#break-even-panel").WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = ChartSettleMilliseconds });
            eyes.Check("Break-even KPI cards",
                Target.Region(page.Locator("#break-even-kpi-grid")).Fully());
            eyes.Check("Break-even input row",
                Target.Region(page.Locator("#break-even-input-row")).Fully());
            eyes.Check("Break-even gauge card",
                Target.Region(page.Locator("#break-even-gauge-card")).Fully());
            eyes.Check("Break-even comparison chart",
                Target.Region(page.Locator("#break-even-chart-card")).Fully());
            await Task.CompletedTask;
        });
    }

    [Fact]
    public async Task Visual_DataDashboardOverviewTile_MatchesBaseline()
    {
        await RunVisualTestAsync("/wiley-workspace", "Dashboard Overview Tile", static async (eyes, page) =>
        {
            eyes.Check("Data Dashboard overview tile",
                Target.Region(page.Locator("#overview-data-dashboard")).Fully());
            await Task.CompletedTask;
        });
    }

    // Additional panel visual coverage

    [Fact]
    public async Task Visual_RatesPanel_MatchesBaseline()
    {
        await RunVisualTestAsync("/wiley-workspace/rates", "Rates Panel - Sections", static async (eyes, page) =>
        {
            await page.Locator("#current-rate-input").WaitForAsync(
                new() { State = WaitForSelectorState.Visible, Timeout = ChartSettleMilliseconds });
            eyes.Check("Rates KPI cards",
                Target.Region(page.Locator("#rates-kpi-grid")).Fully());
            eyes.Check("Rates chart section",
                Target.Region(page.Locator("#rates-panel-chart-section")).Fully());
        });
    }

    [Fact]
    public Task Visual_ScenarioPlannerPanel_MatchesBaseline()
    {
        return RunVisualTestAsync("/wiley-workspace/scenario", "Scenario Planner Panel - Region", static async (eyes, page) =>
        {
            await page.GetByText("Base Break-Even", new() { Exact = true }).WaitForAsync(
                new() { State = WaitForSelectorState.Visible, Timeout = ChartSettleMilliseconds });
            eyes.Check("Full scenario planner panel",
                Target.Region(page.Locator("#scenario-panel")).Fully());
        });
    }

    [Fact]
    public Task Visual_CustomerViewerPanel_MatchesBaseline()
    {
        return RunVisualTestAsync("/wiley-workspace/customers", "Customer Viewer Panel - Region", static async (eyes, page) =>
        {
            await page.GetByText("Visible Customers", new() { Exact = true }).WaitForAsync(
                new() { State = WaitForSelectorState.Visible, Timeout = ChartSettleMilliseconds });
            eyes.Check("Full customer viewer panel",
                Target.Region(page.Locator("#customer-viewer-panel")).Fully());
        });
    }

    [Fact]
    public Task Visual_TrendsPanel_MatchesBaseline()
    {
        return RunVisualTestAsync("/wiley-workspace/trends", "Trends and Projections Panel - Chart", static async (eyes, page) =>
        {
            await page.GetByText("Historical and Projected Rates", new() { Exact = true }).WaitForAsync(
                new() { State = WaitForSelectorState.Visible, Timeout = ChartSettleMilliseconds });
            eyes.Check("Trends projection chart",
                Target.Region(page.Locator("#trends-chart-region")).Fully());
        });
    }

    [Fact]
    public async Task Visual_DecisionSupportPanel_MatchesBaseline()
    {
        await RunVisualTestAsync("/wiley-workspace/decision-support", "Decision Support Panel - Region", static async (eyes, page) =>
        {
            await page.Locator(".jarvis-chat-panel, #decision-support-panel").First.WaitForAsync(
                new() { State = WaitForSelectorState.Visible, Timeout = ChartSettleMilliseconds });
            eyes.Check("Full decision support panel",
                Target.Region(page.Locator("#decision-support-panel")).Fully());
        });
    }

    [Fact]
    public async Task Visual_QuickBooksImportPanel_MatchesBaseline()
    {
        await RunVisualTestAsync("/wiley-workspace/quickbooks-import", "QuickBooks Import Panel - Region", static async (eyes, page) =>
        {
            await page.GetByRole(AriaRole.Button, new() { Name = "Choose QuickBooks file" }).WaitForAsync(
                new() { State = WaitForSelectorState.Visible, Timeout = ChartSettleMilliseconds });
            eyes.Check("Full QuickBooks import panel",
                Target.Region(page.Locator("#quickbooks-import-panel")).Fully());
        });
    }

    [Fact]
    public async Task Visual_RatesPanel_RateComparisonChart_MatchesBaseline()
    {
        await RunVisualTestAsync("/wiley-workspace/rates", "Rates Panel - Chart Region", static async (eyes, page) =>
        {
            await page.GetByText("Rate Comparison", new() { Exact = true }).WaitForAsync(
                new() { State = WaitForSelectorState.Visible, Timeout = ChartSettleMilliseconds });
            eyes.Check("Rate comparison chart region",
                Target.Region(page.Locator("#rates-panel-chart-section")).Fully());
        });
    }

    private async Task RunVisualTestAsync(
        string path,
        string testName,
        Func<Eyes, IPage, Task> visualChecks)
    {
        var baseUrl       = Environment.GetEnvironmentVariable("WILEYCO_E2E_BASE_URL");
        var applitoolsKey = Environment.GetEnvironmentVariable("APPLITOOLS_API_KEY");

        VisualTestHarness.EnsureConfigured(baseUrl, applitoolsKey);

        var settings = ApplitoolsEyesConfiguration.Create(testName, applitoolsKey!);

        var eyes = new Eyes(_runner);
        eyes.SetConfiguration(settings.Configuration);

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        await using var context = await browser.NewContextAsync(new()
        {
            ViewportSize = new() { Width = 1280, Height = 800 }
        });
        await context.AddInitScriptAsync("window.localStorage.clear(); window.sessionStorage.clear();");
        var page = await context.NewPageAsync();
        List<string> consoleMessages = [];
        List<string> pageErrors = [];
        VisualTestHarness.AttachDiagnostics(page, consoleMessages, pageErrors);

        try
        {
            await VisualTestHarness.LoadWorkspaceAsync(
                page,
                baseUrl!,
                path,
                ReadyTimeoutMilliseconds,
                NavigationTimeoutMilliseconds);

            eyes.Open(page, settings.AppName, testName, settings.ViewportSize);
            await visualChecks(eyes, page);
            eyes.Close(false);
        }
        catch (Exception ex) when (ex is not Xunit.Sdk.XunitException)
        {
            eyes.Abort();
            var diagnostics = VisualTestHarness.BuildDiagnostics(page, consoleMessages, pageErrors);
            Assert.Fail($"Visual test failed: {ex.Message}{Environment.NewLine}{diagnostics}");
        }
        // browser and context are disposed by `await using` — no explicit CloseAsync needed
    }
}
