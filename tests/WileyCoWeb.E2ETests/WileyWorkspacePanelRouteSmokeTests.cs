using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace WileyCoWeb.E2ETests;

public sealed class WileyWorkspacePanelRouteSmokeTests
{
    private const int ReadyTimeoutMilliseconds = 90000;
    private const int PanelTimeoutMilliseconds = 30000;

    [Theory]
    [InlineData("/wiley-workspace", "#workspace-overview-dashboard")]
    [InlineData("/wiley-workspace/break-even", "#break-even-panel")]
    [InlineData("/wiley-workspace/rates", "#rates-panel")]
    [InlineData("/wiley-workspace/quickbooks-import", "#quickbooks-import-panel")]
    [InlineData("/wiley-workspace/scenario", "#scenario-panel")]
    [InlineData("/wiley-workspace/customers", "#customer-viewer-panel")]
    [InlineData("/wiley-workspace/trends", "#trends-panel")]
    [InlineData("/wiley-workspace/decision-support", "#decision-support-panel")]
    [InlineData("/wiley-workspace/data-dashboard", "#data-dashboard-panel")]
    public async Task Workspace_PanelRoute_RendersExpectedPanel(string relativePath, string panelSelector)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(panelSelector);

        var baseUrl = Environment.GetEnvironmentVariable("WILEYCO_E2E_BASE_URL");
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return;
        }

        var consoleMessages = new List<string>();
        var pageErrors = new List<string>();

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });

        await using var context = await browser.NewContextAsync();
        await context.AddInitScriptAsync("window.localStorage.clear(); window.sessionStorage.clear();");

        var page = await context.NewPageAsync();
        page.Console += (_, message) => consoleMessages.Add($"{message.Type}: {message.Text}");
        page.PageError += (_, exception) => pageErrors.Add(exception);

        try
        {
            await page.GotoAsync($"{baseUrl.TrimEnd('/')}{relativePath}", new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded
            });

            await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
            await Expect(page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex(relativePath.Replace("/", "\\/")));
            await Expect(page.Locator("#workspace-load-status")).ToContainTextAsync("Workspace ready.", new() { Timeout = ReadyTimeoutMilliseconds });
            await Expect(page.Locator(panelSelector)).ToBeVisibleAsync(new() { Timeout = PanelTimeoutMilliseconds });
        }
        catch (Exception ex)
        {
            var diagnostics = string.Join(Environment.NewLine, [
                $"Route: {relativePath}",
                $"Panel selector: {panelSelector}",
                "Console messages:",
                consoleMessages.Count == 0 ? "  <none>" : string.Join(Environment.NewLine, consoleMessages.Select(message => $"  {message}")),
                "Page errors:",
                pageErrors.Count == 0 ? "  <none>" : string.Join(Environment.NewLine, pageErrors.Select(error => $"  {error}"))
            ]);

            throw new Xunit.Sdk.XunitException($"{ex.Message}{Environment.NewLine}{diagnostics}");
        }
    }

    [Fact]
    public async Task Workspace_OverviewAndSidebarButtons_NavigateToExpectedPanels()
    {
        var baseUrl = Environment.GetEnvironmentVariable("WILEYCO_E2E_BASE_URL");
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return;
        }

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });

        await using var context = await browser.NewContextAsync();
        await context.AddInitScriptAsync("window.localStorage.clear(); window.sessionStorage.clear();");

        var page = await context.NewPageAsync();
        var consoleMessages = new List<string>();
        page.Console += (_, message) => consoleMessages.Add($"{message.Type}: {message.Text}");

        await page.GotoAsync($"{baseUrl.TrimEnd('/')}/wiley-workspace", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded
        });

        await Expect(page.Locator("#workspace-overview-dashboard")).ToBeVisibleAsync(new() { Timeout = PanelTimeoutMilliseconds });

        var navigationCases = new[]
        {
            (OverviewButton: "Open Break-Even", SidebarButton: "Break-Even", PanelUrl: "/wiley-workspace/break-even", PanelSelector: "#break-even-panel"),
            (OverviewButton: "Open Rates", SidebarButton: "Rates", PanelUrl: "/wiley-workspace/rates", PanelSelector: "#rates-panel"),
            (OverviewButton: "Open Import", SidebarButton: "QuickBooks Import", PanelUrl: "/wiley-workspace/quickbooks-import", PanelSelector: "#quickbooks-import-panel"),
            (OverviewButton: "Open Scenario Planner", SidebarButton: "Scenario Planner", PanelUrl: "/wiley-workspace/scenario", PanelSelector: "#scenario-panel"),
            (OverviewButton: "Open Customer Viewer", SidebarButton: "Customer Viewer", PanelUrl: "/wiley-workspace/customers", PanelSelector: "#customer-viewer-panel"),
            (OverviewButton: "Open Trends", SidebarButton: "Trends", PanelUrl: "/wiley-workspace/trends", PanelSelector: "#trends-panel"),
            (OverviewButton: "Open Decision Support", SidebarButton: "Decision Support", PanelUrl: "/wiley-workspace/decision-support", PanelSelector: "#decision-support-panel"),
            (OverviewButton: "Open Data Dashboard", SidebarButton: "Data Dashboard", PanelUrl: "/wiley-workspace/data-dashboard", PanelSelector: "#data-dashboard-panel")
        };

        foreach (var navigationCase in navigationCases)
        {
            await page.GotoAsync($"{baseUrl.TrimEnd('/')}/wiley-workspace", new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded
            });

            await Expect(page.Locator("#workspace-overview-dashboard")).ToBeVisibleAsync(new() { Timeout = PanelTimeoutMilliseconds });

            await page.GetByRole(AriaRole.Button, new() { Name = navigationCase.OverviewButton }).ClickAsync();
            Assert.Contains(consoleMessages, message => message.Contains($"[NAV] Clicked {navigationCase.PanelUrl.Split('/').Last()}", StringComparison.OrdinalIgnoreCase));
            await Expect(page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex(navigationCase.PanelUrl.Replace("/", "\\/")));
            await Expect(page.Locator(navigationCase.PanelSelector)).ToBeVisibleAsync(new() { Timeout = PanelTimeoutMilliseconds });

            await page.GotoAsync($"{baseUrl.TrimEnd('/')}/wiley-workspace", new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded
            });

            await Expect(page.Locator("#workspace-overview-dashboard")).ToBeVisibleAsync(new() { Timeout = PanelTimeoutMilliseconds });

            await page.GetByRole(AriaRole.Button, new() { Name = navigationCase.SidebarButton }).ClickAsync();
            Assert.Contains(consoleMessages, message => message.Contains($"[NAV] Clicked {navigationCase.PanelUrl.Split('/').Last()}", StringComparison.OrdinalIgnoreCase));
            await Expect(page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex(navigationCase.PanelUrl.Replace("/", "\\/")));
            await Expect(page.Locator(navigationCase.PanelSelector)).ToBeVisibleAsync(new() { Timeout = PanelTimeoutMilliseconds });
        }
    }
}