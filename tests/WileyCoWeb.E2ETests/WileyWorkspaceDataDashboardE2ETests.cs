using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace WileyCoWeb.E2ETests;

/// <summary>
/// E2E regression tests for the Data Dashboard panel at /wiley-workspace/data-dashboard.
/// Tests are gated on WILEYCO_E2E_BASE_URL; they no-op when the environment variable is absent.
/// </summary>
public sealed class WileyWorkspaceDataDashboardE2ETests
{
    private const int ReadyTimeoutMilliseconds = 90000;
    private const int ChartTimeoutMilliseconds = 15000;

    [Fact]
    public async Task DataDashboard_NavLink_IsVisible_InSidebar()
    {
        await RunWorkspaceTestAsync(async page =>
        {
            var navLink = page.Locator("a.app-nav-link[href='/wiley-workspace/data-dashboard']");
            await Expect(navLink).ToBeVisibleAsync();
            await Expect(navLink).ToContainTextAsync("Data Dashboard");
        });
    }

    [Fact]
    public async Task DataDashboard_NavLink_NavigatesToPanel()
    {
        await RunWorkspaceTestAsync(async page =>
        {
            var navLink = page.Locator("a.app-nav-link[href='/wiley-workspace/data-dashboard']");
            await navLink.ClickAsync();

            await Expect(page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/wiley-workspace/data-dashboard"));
            await Expect(page.Locator("#data-dashboard-panel")).ToBeVisibleAsync(new() { Timeout = ChartTimeoutMilliseconds });
        });
    }

    [Fact]
    public async Task DataDashboard_DirectUrl_LoadsPanel()
    {
        await RunDashboardTestAsync(async page =>
        {
            await Expect(page.Locator("#data-dashboard-panel")).ToBeVisibleAsync(new() { Timeout = ChartTimeoutMilliseconds });
        });
    }

    [Fact]
    public async Task DataDashboard_Breadcrumb_ShowsPanelLabel()
    {
        await RunDashboardTestAsync(async page =>
        {
            var breadcrumb = page.Locator(".wiley-workspace main .border-b");
            await Expect(breadcrumb).ToContainTextAsync("Data Dashboard");
        });
    }

    [Fact]
    public async Task DataDashboard_KpiCards_AreVisible()
    {
        await RunDashboardTestAsync(async page =>
        {
            await Expect(page.Locator("#kpi-net-position")).ToBeVisibleAsync();
            await Expect(page.Locator("#kpi-coverage-ratio")).ToBeVisibleAsync();
            await Expect(page.Locator("#kpi-rate-adequacy")).ToBeVisibleAsync();
            await Expect(page.Locator("#kpi-scenario-pressure")).ToBeVisibleAsync();
        });
    }

    [Fact]
    public async Task DataDashboard_KpiCards_ContainNumericValues()
    {
        await RunDashboardTestAsync(async page =>
        {
            // Net position card shows a dollar amount
            var netPositionText = await page.Locator("#kpi-net-position").InnerTextAsync();
            Assert.Contains("$", netPositionText, StringComparison.Ordinal);

            // Coverage ratio shows ×
            var coverageText = await page.Locator("#kpi-coverage-ratio").InnerTextAsync();
            Assert.Contains("×", coverageText, StringComparison.Ordinal);

            // Rate adequacy shows %
            var adequacyText = await page.Locator("#kpi-rate-adequacy").InnerTextAsync();
            Assert.Contains("%", adequacyText, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task DataDashboard_CoverageRatioGauge_IsRendered()
    {
        await RunDashboardTestAsync(async page =>
        {
            await Expect(page.Locator("#coverage-ratio-gauge")).ToBeVisibleAsync(new() { Timeout = ChartTimeoutMilliseconds });
        });
    }

    [Fact]
    public async Task DataDashboard_RateAdequacyGauge_IsRendered()
    {
        await RunDashboardTestAsync(async page =>
        {
            await Expect(page.Locator("#rate-adequacy-gauge")).ToBeVisibleAsync(new() { Timeout = ChartTimeoutMilliseconds });
        });
    }

    // Rate comparison is state-computed (always 2 points: "Current" + "Break-Even").
    // This chart is ALWAYS rendered; the assertion is unconditional.
    [Fact]
    public async Task DataDashboard_RateComparisonChart_AlwaysRenders()
    {
        await RunDashboardTestAsync(async page =>
        {
            await Expect(page.Locator("#rate-comparison-section")).ToBeVisibleAsync(new() { Timeout = ChartTimeoutMilliseconds });
            await Expect(page.Locator("#budget-variance-chart")).ToBeVisibleAsync(new() { Timeout = ChartTimeoutMilliseconds });
        });
    }

    // Rate trend is conditional on ProjectionSeries.Count > 0 (API-dependent).
    // Verify the section wrapper first; assert the inner chart only when the section is present.
    [Fact]
    public async Task DataDashboard_RateTrendSection_WhenPresent_ContainsChart()
    {
        await RunDashboardTestAsync(async page =>
        {
            var sectionCount = await page.Locator("#rate-trend-section").CountAsync();
            if (sectionCount == 0)
                return; // No projection rows from API — section correctly absent.

            await Expect(page.Locator("#rate-trend-section")).ToBeVisibleAsync();
            await Expect(page.Locator("#rate-trend-chart")).ToBeVisibleAsync(new() { Timeout = ChartTimeoutMilliseconds });
        });
    }

    // Waterfall is conditional on ScenarioItems.Count > 0.
    [Fact]
    public async Task DataDashboard_WaterfallSection_WhenPresent_ContainsChart()
    {
        await RunDashboardTestAsync(async page =>
        {
            var sectionCount = await page.Locator("#waterfall-section").CountAsync();
            if (sectionCount == 0)
                return; // No scenario items — section correctly absent.

            await Expect(page.Locator("#waterfall-section")).ToBeVisibleAsync();
            await Expect(page.Locator("#scenario-waterfall-chart")).ToBeVisibleAsync(new() { Timeout = ChartTimeoutMilliseconds });
        });
    }

    // Customer donuts are conditional on at least one CustomerRow with data.
    // Both charts must be inside the same section wrapper when the section exists.
    [Fact]
    public async Task DataDashboard_CustomerDonutsSection_WhenPresent_ContainsBothCharts()
    {
        await RunDashboardTestAsync(async page =>
        {
            var sectionCount = await page.Locator("#customer-donuts-section").CountAsync();
            if (sectionCount == 0)
                return; // No customer rows — section correctly absent.

            await Expect(page.Locator("#customer-donuts-section")).ToBeVisibleAsync();
            await Expect(page.Locator("#customer-service-chart")).ToBeVisibleAsync(new() { Timeout = ChartTimeoutMilliseconds });
            await Expect(page.Locator("#customer-citylimits-chart")).ToBeVisibleAsync(new() { Timeout = ChartTimeoutMilliseconds });
        });
    }

    // Data-binding verification: assert real content, not just element presence.
    [Fact]
    public async Task DataDashboard_KpiNetPosition_SubtitleContainsFiscalYear()
    {
        await RunDashboardTestAsync(async page =>
        {
            // Subtitle format is "<enterprise> · FY<year>" — proves data-bound render.
            var subtitleText = await page.Locator("#kpi-net-position .e-card-sub-title").InnerTextAsync();
            Assert.Contains("FY", subtitleText, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task DataDashboard_OverviewTile_IsVisibleOnOverviewPage()
    {
        await RunWorkspaceTestAsync(async page =>
        {
            // Overview tile renders on the main overview dashboard
            var tile = page.Locator("#overview-data-dashboard");
            await Expect(tile).ToBeVisibleAsync();
            await Expect(tile).ToContainTextAsync("Data Dashboard");
        });
    }

    [Fact]
    public async Task DataDashboard_OverviewTile_OpenFullView_NavigatesToPanel()
    {
        await RunWorkspaceTestAsync(async page =>
        {
            var openButton = page.Locator("#overview-data-dashboard a[href='/wiley-workspace/data-dashboard']").First;
            await openButton.ClickAsync();

            await Expect(page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/wiley-workspace/data-dashboard"));
            await Expect(page.Locator("#data-dashboard-panel")).ToBeVisibleAsync(new() { Timeout = ChartTimeoutMilliseconds });
        });
    }

    [Fact]
    public async Task DataDashboard_LoadsWithoutUnhandledPageErrors()
    {
        await RunDashboardTestAsync(async page =>
        {
            // Wait for chart-heavy elements to settle using stable locators — no fragile timing wait.
            await Expect(page.Locator("#coverage-ratio-gauge")).ToBeVisibleAsync(new() { Timeout = ChartTimeoutMilliseconds });
            await Expect(page.Locator("#rate-adequacy-gauge")).ToBeVisibleAsync(new() { Timeout = ChartTimeoutMilliseconds });
            await Expect(page.Locator("#budget-variance-chart")).ToBeVisibleAsync(new() { Timeout = ChartTimeoutMilliseconds });
        }, collectErrors: true);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    // Navigate to the workspace overview and run the test body.
    private static Task RunWorkspaceTestAsync(Func<IPage, Task> testBody) =>
        RunE2EAsync("/wiley-workspace", testBody, collectErrors: false);

    // Navigate directly to the Data Dashboard panel and run the test body.
    private static Task RunDashboardTestAsync(Func<IPage, Task> testBody, bool collectErrors = false) =>
        RunE2EAsync("/wiley-workspace/data-dashboard", testBody, collectErrors);

    private static async Task RunE2EAsync(string path, Func<IPage, Task> testBody, bool collectErrors)
    {
        ArgumentNullException.ThrowIfNull(testBody);

        var baseUrl = Environment.GetEnvironmentVariable("WILEYCO_E2E_BASE_URL");
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return; // E2E gate — skip when no target URL is configured
        }

        var consoleErrors = new List<string>();
        var pageErrors = new List<string>();
        var networkEvents = new List<string>();

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });

        await using var context = await browser.NewContextAsync();
        await context.AddInitScriptAsync("window.localStorage.clear(); window.sessionStorage.clear();");

        var page = await context.NewPageAsync();
        page.Request += (_, request) =>
        {
            if (ShouldCaptureNetworkEvent(request.Url))
            {
                networkEvents.Add($"> {request.Method} {request.Url}");
            }
        };
        page.Response += (_, response) =>
        {
            if (ShouldCaptureNetworkEvent(response.Url))
            {
                networkEvents.Add($"< {(int)response.Status} {response.Url}");
            }
        };
        page.RequestFailed += (_, request) =>
        {
            if (ShouldCaptureNetworkEvent(request.Url))
            {
                networkEvents.Add($"! {request.Method} {request.Url} :: {request.Failure}");
            }
        };
        page.Console += (_, msg) =>
        {
            if (collectErrors || msg.Type is "error")
            {
                consoleErrors.Add($"{msg.Type}: {msg.Text}");
            }
        };
        page.PageError += (_, ex) => pageErrors.Add(ex);

        try
        {
            await page.GotoAsync($"{baseUrl.TrimEnd('/')}{path}", new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded
            });

            await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
            await Expect(page.Locator("#workspace-load-status"))
                .ToContainTextAsync("Workspace ready.", new() { Timeout = ReadyTimeoutMilliseconds });

            await testBody(page);

            if (collectErrors && pageErrors.Count > 0)
                Assert.Fail($"Unhandled page errors: {string.Join("; ", pageErrors)}");
        }
        catch (Exception ex) when (ex is not Xunit.Sdk.XunitException)
        {
            var diagnostics = BuildDiagnostics(page, consoleErrors, pageErrors, networkEvents);
            Assert.Fail($"E2E test failed: {ex.Message}{Environment.NewLine}{diagnostics}");
        }

        // browser and context are disposed by `await using` — no explicit CloseAsync needed
    }

    private static string BuildDiagnostics(
        IPage page,
        IReadOnlyCollection<string> consoleMessages,
        IReadOnlyCollection<string> pageErrors,
        IReadOnlyCollection<string> networkEvents)
    {
        var safeConsoleMessages = consoleMessages.Count > 0
            ? consoleMessages.Select(message => $"- {message}")
            : ["- <none>"];
        var safePageErrors = pageErrors.Count > 0
            ? pageErrors.Select(error => $"- {error}")
            : ["- <none>"];
        var safeNetworkEvents = networkEvents.Count > 0
            ? networkEvents.Select(entry => $"- {entry}")
            : ["- <none>"];

        return string.Join(Environment.NewLine, [
            $"Page URL: {page.Url}",
            "Console messages:",
            .. safeConsoleMessages,
            "Page errors:",
            .. safePageErrors,
            "Network events:",
            .. safeNetworkEvents
        ]);
    }

    private static bool ShouldCaptureNetworkEvent(string url)
    {
        return url.Contains("appsettings.Workspace.local.json", StringComparison.OrdinalIgnoreCase)
            || url.Contains("/api/workspace/", StringComparison.OrdinalIgnoreCase)
            || url.Contains("execute-api", StringComparison.OrdinalIgnoreCase)
            || url.Contains("awsapprunner.com", StringComparison.OrdinalIgnoreCase);
    }
}
