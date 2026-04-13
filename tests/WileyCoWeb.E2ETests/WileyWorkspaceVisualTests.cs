using Applitools;
using Applitools.Playwright;
using Applitools.Playwright.Fluent;
using Applitools.Utils.Geometry;
using Microsoft.Playwright;

namespace WileyCoWeb.E2ETests;

/// <summary>
/// Applitools Eyes visual regression tests for Wiley Widget Syncfusion panels.
/// Tests gate on WILEYCO_E2E_BASE_URL and APPLITOOLS_API_KEY — both must be set.
/// On first run, Eyes captures baselines. Subsequent runs compare against them.
/// Results visible at https://eyes.applitools.com
/// </summary>
public sealed class WileyWorkspaceVisualTests : IDisposable
{
    private const int ReadyTimeoutMilliseconds = 90_000;
    private const int ChartSettleMilliseconds  = 15_000;

    private readonly ClassicRunner _runner = new();

    public void Dispose()
    {
        _ = _runner.GetAllTestResults(false);
    }

    [Fact]
    public async Task Visual_WorkspaceOverview_MatchesBaseline()
    {
        await RunVisualTestAsync("/wiley-workspace", "Workspace Overview", async (eyes, page) =>
        {
            eyes.Check("Full overview dashboard", Target.Window().Fully());
            await Task.CompletedTask;
        });
    }

    [Fact]
    public async Task Visual_NavMenu_MatchesBaseline()
    {
        await RunVisualTestAsync("/wiley-workspace", "Nav Menu", async (eyes, page) =>
        {
            eyes.Check("Sidebar navigation",
                Target.Region(page.Locator(".sidebar-nav, nav, .e-sidebar")).Fully());
            await Task.CompletedTask;
        });
    }

    [Fact]
    public async Task Visual_DataDashboard_KpiCards_MatchBaseline()
    {
        await RunVisualTestAsync("/wiley-workspace/data-dashboard", "Data Dashboard – KPI Cards", async (eyes, page) =>
        {
            await page.Locator("#coverage-ratio-gauge").WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = ChartSettleMilliseconds });
            eyes.Check("KPI card row",
                Target.Region(page.Locator("#kpi-net-position, #kpi-coverage-ratio, #kpi-rate-adequacy, #kpi-scenario-pressure")).Fully());
        });
    }

    [Fact]
    public async Task Visual_DataDashboard_CoverageGauge_MatchesBaseline()
    {
        await RunVisualTestAsync("/wiley-workspace/data-dashboard", "Data Dashboard – Coverage Gauge", async (eyes, page) =>
        {
            await page.Locator("#coverage-ratio-gauge").WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = ChartSettleMilliseconds });
            eyes.Check("Coverage ratio circular gauge",
                Target.Region(page.Locator("#coverage-ratio-gauge")).Fully());
        });
    }

    [Fact]
    public async Task Visual_DataDashboard_RateAdequacyGauge_MatchesBaseline()
    {
        await RunVisualTestAsync("/wiley-workspace/data-dashboard", "Data Dashboard – Rate Adequacy Gauge", async (eyes, page) =>
        {
            await page.Locator("#rate-adequacy-gauge").WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = ChartSettleMilliseconds });
            eyes.Check("Rate adequacy circular gauge",
                Target.Region(page.Locator("#rate-adequacy-gauge")).Fully());
        });
    }

    [Fact]
    public async Task Visual_DataDashboard_RateComparisonChart_MatchesBaseline()
    {
        await RunVisualTestAsync("/wiley-workspace/data-dashboard", "Data Dashboard – Rate Comparison", async (eyes, page) =>
        {
            await page.Locator("#budget-variance-chart").WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = ChartSettleMilliseconds });
            eyes.Check("Rate comparison bar chart",
                Target.Region(page.Locator("#rate-comparison-section")).Fully());
        });
    }

    [Fact]
    public async Task Visual_DataDashboard_FullPanel_MatchesBaseline()
    {
        await RunVisualTestAsync("/wiley-workspace/data-dashboard", "Data Dashboard – Full Panel", async (eyes, page) =>
        {
            await page.Locator("#budget-variance-chart").WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = ChartSettleMilliseconds });
            eyes.Check("Full data dashboard panel", Target.Window().Fully());
        });
    }

    [Fact]
    public async Task Visual_BreakEvenPanel_MatchesBaseline()
    {
        await RunVisualTestAsync("/wiley-workspace/break-even", "Break-Even Panel", async (eyes, page) =>
        {
            eyes.Check("Break-even panel", Target.Window().Fully());
            await Task.CompletedTask;
        });
    }

    [Fact]
    public async Task Visual_DataDashboardOverviewTile_MatchesBaseline()
    {
        await RunVisualTestAsync("/wiley-workspace", "Dashboard Overview Tile", async (eyes, page) =>
        {
            eyes.Check("Data Dashboard overview tile",
                Target.Region(page.Locator("#overview-data-dashboard")).Fully());
            await Task.CompletedTask;
        });
    }

    private async Task RunVisualTestAsync(
        string path,
        string testName,
        Func<Eyes, IPage, Task> visualChecks)
    {
        var baseUrl       = Environment.GetEnvironmentVariable("WILEYCO_E2E_BASE_URL");
        var applitoolsKey = Environment.GetEnvironmentVariable("APPLITOOLS_API_KEY");

        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(applitoolsKey))
            return;

        var config = new Configuration();
        config.SetBatch(new BatchInfo("Wiley Widget Visual Suite"));
        config.SetAppName("Wiley Widget");
        config.SetTestName(testName);
        config.SetViewportSize(new RectangleSize(1280, 800));
        config.SetApiKey(applitoolsKey);

        var eyes = new Eyes(_runner);
        eyes.SetConfiguration(config);

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        await using var context = await browser.NewContextAsync(new()
        {
            ViewportSize = new() { Width = 1280, Height = 800 }
        });
        await context.AddInitScriptAsync("window.localStorage.clear();");
        var page = await context.NewPageAsync();

        try
        {
            await page.GotoAsync(
                $"{baseUrl.TrimEnd('/')}{path}",
                new() { WaitUntil = WaitUntilState.DOMContentLoaded });

            await page.WaitForSelectorAsync(
                "#workspace-load-status:has-text('Workspace ready.')",
                new() { Timeout = ReadyTimeoutMilliseconds });

            eyes.Open(page, "Wiley Widget", testName, new RectangleSize(1280, 800));
            await visualChecks(eyes, page);
            eyes.Close(false);
        }
        catch (Exception ex) when (ex is not Xunit.Sdk.XunitException)
        {
            eyes.Abort();
            Assert.Fail($"Visual test failed: {ex.Message}");
        }
        finally
        {
            await browser.CloseAsync();
        }
    }
}
