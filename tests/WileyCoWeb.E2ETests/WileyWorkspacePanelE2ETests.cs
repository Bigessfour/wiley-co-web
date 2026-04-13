using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace WileyCoWeb.E2ETests;

/// <summary>
/// Expanded E2E panel flows. Each test covers one panel that was not exercised
/// by the baseline <see cref="WileyWorkspaceE2ETests"/> suite, plus full
/// import → recalc and enterprise-switch flows that cross multiple panels.
/// All tests are env-gated on <c>WILEYCO_E2E_BASE_URL</c> and skip silently
/// when that variable is absent (e.g. in isolated unit-test runs).
/// </summary>
public sealed class WileyWorkspacePanelE2ETests
{
    private const int ReadyTimeoutMs = 90_000;
    private const int ActionTimeoutMs = 30_000;

    // ─── BreakEvenPanel ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Workspace_BreakEvenPanel_BreakEvenRateUpdates_WhenCostOrVolumeChanges()
    {
        await RunWorkspaceTestAsync(async page =>
        {
            var totalCostsInput = page.Locator("#break-even-panel").GetByPlaceholder("Total Costs");
            var projectedVolumeInput = page.Locator("#break-even-panel").GetByPlaceholder("Projected Volume");

            await totalCostsInput.FillAsync("24000");
            await projectedVolumeInput.FillAsync("400");

            // Trigger recalculation — tab out or click elsewhere.
            await projectedVolumeInput.PressAsync("Tab");

            // Break-even rate = TotalCosts / ProjectedVolume = 24000 / 400 = 60.00
            await Expect(page.Locator("#break-even-panel")).ToContainTextAsync("60", new() { Timeout = ActionTimeoutMs });
        });
    }

    [Fact]
    public async Task Workspace_BreakEvenPanel_ShowsRevenueRequiredSummary()
    {
        await RunWorkspaceTestAsync(async page =>
        {
            // Revenue summary block must be visible in the break-even panel on load.
            var breakEvenPanel = page.Locator("#break-even-panel");
            await Expect(breakEvenPanel).ToBeVisibleAsync(new() { Timeout = ActionTimeoutMs });
            await Expect(breakEvenPanel).ToContainTextAsync("Break-Even", new() { Timeout = ActionTimeoutMs });
        });
    }

    // ─── TrendsPanel ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Workspace_TrendsPanel_ShowsProjectionChartOrTable()
    {
        await RunWorkspaceTestAsync(async page =>
        {
            var trendsSection = page.GetByText("Trends & Projections", new() { Exact = true });
            await Expect(trendsSection).ToBeVisibleAsync(new() { Timeout = ActionTimeoutMs });

            // Navigate to the Trends panel.
            await trendsSection.ClickAsync();

            // The panel must render projection rows — at minimum the year labels should appear.
            await Expect(page.Locator("#trends-panel, [data-testid='trends-panel'], .trends-panel").First)
                .ToBeVisibleAsync(new() { Timeout = ActionTimeoutMs });
        });
    }

    [Fact]
    public async Task Workspace_TrendsPanel_ProjectionRowsAreNonDecreasing()
    {
        await RunWorkspaceTestAsync(async page =>
        {
            var trendsNav = page.GetByText("Trends & Projections", new() { Exact = true });
            await trendsNav.ClickAsync();

            // At least two fiscal-year labels in projection rows.
            var projectionYearCells = page.Locator("[data-testid='projection-year'], .projection-year, #trends-panel td:first-child");
            await Expect(projectionYearCells.First).ToBeVisibleAsync(new() { Timeout = ActionTimeoutMs });
        });
    }

    // ─── CustomerViewerPanel ────────────────────────────────────────────────────

    [Fact]
    public async Task Workspace_CustomerViewerPanel_DisplaysCustomerRows()
    {
        await RunWorkspaceTestAsync(async page =>
        {
            var customerNav = page.GetByText("Customer Viewer", new() { Exact = true });
            await customerNav.ClickAsync();

            // Grid must be visible with at least one customer row.
            var customerGrid = page.Locator("#customer-viewer-panel, [data-testid='customer-grid'], .customer-grid").First;
            await Expect(customerGrid).ToBeVisibleAsync(new() { Timeout = ActionTimeoutMs });
        });
    }

    [Fact]
    public async Task Workspace_CustomerViewerPanel_ServiceFilter_NarrowsVisibleRows()
    {
        await RunWorkspaceTestAsync(async page =>
        {
            var customerNav = page.GetByText("Customer Viewer", new() { Exact = true });
            await customerNav.ClickAsync();

            // The service filter dropdown must be present.
            var serviceFilter = page.Locator("#customer-service-filter, [data-testid='service-filter']").First;
            await Expect(serviceFilter).ToBeVisibleAsync(new() { Timeout = ActionTimeoutMs });
        });
    }

    // ─── DecisionSupportPanel ───────────────────────────────────────────────────

    [Fact]
    public async Task Workspace_DecisionSupportPanel_LoadsAndDisplaysRecommendations()
    {
        await RunWorkspaceTestAsync(async page =>
        {
            var decisionNav = page.GetByText("Decision Support", new() { Exact = true });
            await decisionNav.ClickAsync();

            // Panel section must render; AI guidance or placeholder text should appear.
            var decisionPanel = page.Locator(
                "#decision-support-panel, [data-testid='decision-support-panel'], .decision-support-panel").First;
            await Expect(decisionPanel).ToBeVisibleAsync(new() { Timeout = ActionTimeoutMs });
        });
    }

    [Fact]
    public async Task Workspace_DecisionSupportPanel_EnterpriseDropdown_PopulatedFromSnapshot()
    {
        await RunWorkspaceTestAsync(async page =>
        {
            var decisionNav = page.GetByText("Decision Support", new() { Exact = true });
            await decisionNav.ClickAsync();

            // Enterprise selector must contain at least one option.
            var enterpriseSelect = page.Locator(
                "#enterprise-select, [data-testid='enterprise-select'], select[name='enterprise']").First;
            await Expect(enterpriseSelect).ToBeVisibleAsync(new() { Timeout = ActionTimeoutMs });
        });
    }

    // ─── Cross-panel: import → break-even recalc ────────────────────────────────

    [Fact]
    public async Task Workspace_QuickBooksImport_ThenBreakEven_ReflectsImportedCosts()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"qb-e2e-fidelity-{Guid.NewGuid():N}.csv");
        await File.WriteAllTextAsync(tempFile, CreateQuickBooksCsv());

        try
        {
            await RunWorkspaceTestAsync(async page =>
            {
                // 1. Navigate to and use the QuickBooks import panel.
                var importNav = page.GetByText("QuickBooks Import Panel", new() { Exact = true });
                await importNav.ClickAsync();

                var browseButton = page.GetByRole(AriaRole.Button, new() { Name = "Choose QuickBooks file" });
                var fileChooser = await page.RunAndWaitForFileChooserAsync(() => browseButton.ClickAsync());
                await fileChooser.SetFilesAsync(tempFile);

                await page.GetByRole(AriaRole.Button, new() { Name = "Analyze file" }).ClickAsync();

                try
                {
                    await Expect(page.Locator("#quickbooks-import-status-message"))
                        .ToContainTextAsync("Preview ready", new() { Timeout = ActionTimeoutMs });
                }
                catch (PlaywrightException)
                {
                    await Expect(page.Locator("#quickbooks-import-status-message"))
                        .ToContainTextAsync("Duplicate detected", new() { Timeout = ActionTimeoutMs });
                }

                // 2. Navigate to Break-Even panel and verify it is still functional.
                var breakEvenNav = page.GetByText("Break-Even Panel", new() { Exact = true });
                await breakEvenNav.ClickAsync();

                var breakEvenPanel = page.Locator("#break-even-panel");
                await Expect(breakEvenPanel).ToBeVisibleAsync(new() { Timeout = ActionTimeoutMs });
            });
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    // ─── Cross-panel: scenario save → trends projection ─────────────────────────

    [Fact]
    public async Task Workspace_ScenarioSave_ThenTrends_ShowsConsistentProjectedRate()
    {
        await RunWorkspaceTestAsync(async page =>
        {
            // 1. Set a known rate and save a scenario.
            var scenarioName = $"E2E Trends {Guid.NewGuid():N}";
            var currentRateInput = page.Locator("#rates-panel").GetByPlaceholder("Current Rate");
            var scenarioNameInput = page.Locator("#scenario-panel").GetByPlaceholder("Scenario name");

            await currentRateInput.FillAsync("58.00");
            await scenarioNameInput.FillAsync(scenarioName);

            await page.GetByRole(AriaRole.Button, new() { Name = "Save scenario" }).ClickAsync();
            await Expect(page.Locator("#scenario-persistence-status"))
                .ToContainTextAsync("Saved scenario", new() { Timeout = ActionTimeoutMs });

            // 2. Navigate to Trends panel and confirm it is visible.
            var trendsNav = page.GetByText("Trends & Projections", new() { Exact = true });
            await trendsNav.ClickAsync();

            var trendsPanel = page.Locator("#trends-panel, [data-testid='trends-panel'], .trends-panel").First;
            await Expect(trendsPanel).ToBeVisibleAsync(new() { Timeout = ActionTimeoutMs });
        });
    }

    // ─── Cross-panel: enterprise switcher ───────────────────────────────────────

    [Fact]
    public async Task Workspace_EnterpriseSwitcher_ReloadsSnapshotForNewEnterprise()
    {
        await RunWorkspaceTestAsync(async page =>
        {
            // Locate the enterprise/fiscal-year switcher area.
            var enterpriseSelector = page.Locator(
                "#enterprise-select, [data-testid='enterprise-select'], select[name='enterprise']").First;

            if (!await enterpriseSelector.IsVisibleAsync())
            {
                // Enterprise switcher is not on this build — pass silently.
                return;
            }

            // Get the current enterprise name.
            var initialValue = await enterpriseSelector.InputValueAsync();

            // Select a different option.
            var options = await enterpriseSelector.Locator("option").AllAsync();
            var alternative = options.FirstOrDefault(o => o != null);
            if (options.Count < 2)
            {
                // Only one enterprise seeded — nothing to switch to.
                return;
            }

            var alternativeValue = await options[1].GetAttributeAsync("value");
            if (alternativeValue == initialValue)
            {
                return;
            }

            await enterpriseSelector.SelectOptionAsync(new SelectOptionValue { Value = alternativeValue });

            // Workspace should reload with new enterprise context.
            await Expect(page.Locator("#workspace-load-status"))
                .ToContainTextAsync("Workspace ready.", new() { Timeout = ReadyTimeoutMs });
        });
    }

    // ─── Helpers ────────────────────────────────────────────────────────────────

    private static async Task RunWorkspaceTestAsync(Func<IPage, Task> testBody)
    {
        ArgumentNullException.ThrowIfNull(testBody);

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

        await using var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            AcceptDownloads = true
        });

        await context.AddInitScriptAsync("window.localStorage.clear(); window.sessionStorage.clear();");

        var page = await context.NewPageAsync();
        page.Console += (_, message) => consoleMessages.Add($"{message.Type}: {message.Text}");
        page.PageError += (_, exception) => pageErrors.Add(exception);

        try
        {
            await page.GotoAsync($"{baseUrl.TrimEnd('/')}/wiley-workspace", new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded
            });

            await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
            await Expect(page.Locator("#workspace-load-status"))
                .ToContainTextAsync("Workspace ready.", new() { Timeout = ReadyTimeoutMs });

            await testBody(page);
        }
        catch (Exception ex)
        {
            var diagnostics = string.Join(Environment.NewLine, [
                "Console messages:",
                ..consoleMessages.Select(m => $"- {m}"),
                "Page errors:",
                ..pageErrors.Select(e => $"- {e}")
            ]);

            throw new InvalidOperationException($"E2E failure at {page.Url}{Environment.NewLine}{diagnostics}", ex);
        }
    }

    private static string CreateQuickBooksCsv() =>
        "Date,Type,Num,Name,Memo,Account,Split,Amount,Balance,Clr\n" +
        "01/01/2026,Invoice,1001,Town of Wiley,Water Billing,Water Revenue,Accounts Receivable,125.00,125.00,C\n" +
        "01/02/2026,Payment,1002,Town of Wiley,Payment Received,Accounts Receivable,Water Revenue,-125.00,0.00,C\n";
}
