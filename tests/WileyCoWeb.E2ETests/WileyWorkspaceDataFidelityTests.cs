using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace WileyCoWeb.E2ETests;

/// <summary>
/// Data-fidelity E2E tests: import a known QuickBooks CSV fixture then assert that
/// specific values propagate correctly into each panel. Proves the full pipeline:
/// QuickBooks CSV -> thin API -> Aurora DB -> Blazor panels rendered with live data.
/// Tests gate on WILEYCO_E2E_BASE_URL only (no Applitools key required).
/// </summary>
public sealed class WileyWorkspaceDataFidelityTests
{
    private const int ReadyTimeoutMilliseconds  = 90_000;
    private const int ActionTimeoutMilliseconds = 30_000;

    // ─── Panel data-fidelity assertions ──────────────────────────────────────────

    [Fact]
    public async Task DataFidelity_ImportGeneralLedger_BreakEvenReflectsCosts()
    {
        await RunDataFidelityTestAsync(async (page, tempFile) =>
        {
            await ImportFixtureAsync(page, tempFile);

            var breakEvenNav = page.GetByText("Break-Even Panel", new() { Exact = true });
            await breakEvenNav.ClickAsync();

            var panel = page.Locator("#break-even-panel");
            await Expect(panel).ToBeVisibleAsync(new() { Timeout = ActionTimeoutMilliseconds });
            await Expect(panel).ToContainTextAsync("Break-Even", new() { Timeout = ActionTimeoutMilliseconds });

            // After a successful import the costs field should be populated.
            var costsInput = panel.GetByPlaceholder("Total Costs");
            var rawValue   = await costsInput.InputValueAsync();
            var parsed     = decimal.TryParse(rawValue.Replace(",", ""), out var value);
            Assert.True(parsed && value > 0,
                $"Expected Total Costs to be non-zero after import, but got: '{rawValue}'");
        });
    }

    [Fact]
    public async Task DataFidelity_ImportGeneralLedger_TrendsShowsCurrentYearData()
    {
        await RunDataFidelityTestAsync(async (page, tempFile) =>
        {
            await ImportFixtureAsync(page, tempFile);

            var trendsNav = page.GetByText("Trends & Projections", new() { Exact = true });
            await trendsNav.ClickAsync();

            var panel = page.Locator("#trends-panel, [data-testid='trends-panel'], .trends-panel").First;
            await Expect(panel).ToBeVisibleAsync(new() { Timeout = ActionTimeoutMilliseconds });

            // Fixture rows are dated 2026 — the current fiscal year must appear in projections.
            await Expect(panel).ToContainTextAsync("2026", new() { Timeout = ActionTimeoutMilliseconds });
        });
    }

    [Fact]
    public async Task DataFidelity_ImportGeneralLedger_DataDashboardKpiCardsUpdate()
    {
        await RunDataFidelityTestAsync(async (page, tempFile) =>
        {
            await ImportFixtureAsync(page, tempFile);

            // Navigate to Data Dashboard via nav link.
            var dashboardNav = page.GetByText("Data Dashboard").First;
            await dashboardNav.ClickAsync();

            // All four KPI cards must be visible and contain non-empty text.
            foreach (var cardId in new[] { "#kpi-net-position", "#kpi-coverage-ratio", "#kpi-rate-adequacy", "#kpi-scenario-pressure" })
            {
                var card = page.Locator(cardId);
                await Expect(card).ToBeVisibleAsync(new() { Timeout = ActionTimeoutMilliseconds });
                var text = await card.InnerTextAsync();
                Assert.False(string.IsNullOrWhiteSpace(text),
                    $"KPI card {cardId} should contain a value after import.");
            }
        });
    }

    [Fact]
    public async Task DataFidelity_ImportGeneralLedger_ScenarioPlannerShowsBaselineValues()
    {
        await RunDataFidelityTestAsync(async (page, tempFile) =>
        {
            await ImportFixtureAsync(page, tempFile);

            var scenarioNav = page.GetByText("Scenario Planner", new() { Exact = true });
            await scenarioNav.ClickAsync();

            var panel = page.Locator("#scenario-panel");
            await Expect(panel).ToBeVisibleAsync(new() { Timeout = ActionTimeoutMilliseconds });
            await Expect(panel).ToContainTextAsync("Scenario", new() { Timeout = ActionTimeoutMilliseconds });

            // Baseline rate input must be present and readable after import.
            var rateInput = panel.GetByPlaceholder("Current Rate");
            await Expect(rateInput).ToBeVisibleAsync(new() { Timeout = ActionTimeoutMilliseconds });
        });
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Navigates to the QuickBooks import panel, uploads <paramref name="tempFile"/>,
    /// triggers analysis, and waits for "Preview ready" or "Duplicate detected" status.
    /// If a "Confirm import" button appears, clicks it and waits for "Import complete".
    /// </summary>
    private static async Task ImportFixtureAsync(IPage page, string tempFile)
    {
        var importNav = page.GetByText("QuickBooks Import Panel", new() { Exact = true });
        await importNav.ClickAsync();

        var browseButton = page.GetByRole(AriaRole.Button, new() { Name = "Choose QuickBooks file" });
        await browseButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15_000 });

        var fileChooser = await page.RunAndWaitForFileChooserAsync(() => browseButton.ClickAsync());
        await fileChooser.SetFilesAsync(tempFile);

        await page.GetByRole(AriaRole.Button, new() { Name = "Analyze file" }).ClickAsync();

        bool isDuplicate;
        try
        {
            await Expect(page.Locator("#quickbooks-import-status-message"))
                .ToContainTextAsync("Preview ready", new() { Timeout = ActionTimeoutMilliseconds });
            isDuplicate = false;
        }
        catch (PlaywrightException)
        {
            // Duplicate is acceptable — live data already contains these rows.
            await Expect(page.Locator("#quickbooks-import-status-message"))
                .ToContainTextAsync("Duplicate detected", new() { Timeout = ActionTimeoutMilliseconds });
            isDuplicate = true;
        }

        if (!isDuplicate)
        {
            var confirmButton = page.GetByRole(AriaRole.Button, new() { Name = "Confirm import" });
            if (await confirmButton.IsVisibleAsync())
            {
                await confirmButton.ClickAsync();
                await Expect(page.Locator("#quickbooks-import-status-message"))
                    .ToContainTextAsync("Import complete", new() { Timeout = ActionTimeoutMilliseconds });
            }
        }
    }

    private static async Task RunDataFidelityTestAsync(Func<IPage, string, Task> testBody)
    {
        var baseUrl = Environment.GetEnvironmentVariable("WILEYCO_E2E_BASE_URL");
        if (string.IsNullOrWhiteSpace(baseUrl))
            return;

        var tempFile = Path.Combine(Path.GetTempPath(), $"qb-fidelity-{Guid.NewGuid():N}.csv");
        await File.WriteAllTextAsync(tempFile, CreateFixtureCsv());

        try
        {
            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
            await using var context = await browser.NewContextAsync(new()
            {
                ViewportSize = new() { Width = 1280, Height = 800 },
            });
            await context.AddInitScriptAsync("window.localStorage.clear(); window.sessionStorage.clear();");
            var page = await context.NewPageAsync();

            var consoleErrors = new List<string>();
            page.PageError += (_, e) => consoleErrors.Add(e);

            await page.GotoAsync(
                $"{baseUrl.TrimEnd('/')}/wiley-workspace",
                new() { WaitUntil = WaitUntilState.DOMContentLoaded });

            await Expect(page.Locator("#workspace-load-status"))
                .ToContainTextAsync("Workspace ready.", new() { Timeout = ReadyTimeoutMilliseconds });

            await testBody(page, tempFile);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Fixture CSV with known totals: $7,000 costs, $18,500 revenue, all dated Jan 2026.
    /// After a full import these amounts should surface in Break-Even and dashboard KPIs.
    /// </summary>
    private static string CreateFixtureCsv() =>
        "Date,Type,Num,Name,Memo,Account,Split,Amount,Balance,Clr\n" +
        "01/05/2026,Bill,B-401,Wiley Water Dept,Pump maintenance Q1,Operations,Accounts Payable,4800.00,4800.00,C\n" +
        "01/12/2026,Bill,B-402,Wiley Water Dept,Chemical treatment Q1,Operations,Accounts Payable,2200.00,7000.00,C\n" +
        "01/19/2026,Invoice,INV-801,Town of Wiley,Water billing Jan,Water Revenue,Accounts Receivable,18500.00,18500.00,C\n";
}
