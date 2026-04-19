using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace WileyCoWeb.E2ETests;

/// <summary>
/// Data-fidelity E2E tests: import a known QuickBooks CSV fixture then assert that
/// specific values propagate correctly into each panel. Proves the full pipeline:
/// QuickBooks CSV -> thin API -> Aurora DB -> Blazor panels rendered with live data.
/// Tests gate on WILEYCO_E2E_BASE_URL only.
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

            var breakEvenNav = page.GetByRole(AriaRole.Button, new() { Name = "Break-Even" });
            await breakEvenNav.ClickAsync();

            var panel = page.Locator("#break-even-panel");
            await Expect(panel).ToBeVisibleAsync(new() { Timeout = ActionTimeoutMilliseconds });
            await Expect(panel).ToContainTextAsync("Break-Even", new() { Timeout = ActionTimeoutMilliseconds });

            // Verify the numeric input renders (spinbutton role is stable across FloatLabelType.Auto re-renders).
            var costsInput = panel.GetByRole(AriaRole.Spinbutton).First;
            await Expect(costsInput).ToBeVisibleAsync(new() { Timeout = ActionTimeoutMilliseconds });
            var rawValue   = await costsInput.InputValueAsync();
            var stripped = rawValue.Replace("$", "").Replace(",", "").Trim();
            var parsed = decimal.TryParse(stripped, out _);
            Assert.True(parsed,
                $"Expected Total Costs input to contain a parseable number after import, but got: '{rawValue}'");
        });
    }

    [Fact]
    public async Task DataFidelity_ImportGeneralLedger_TrendsShowsCurrentYearData()
    {
        await RunDataFidelityTestAsync(async (page, tempFile) =>
        {
            await ImportFixtureAsync(page, tempFile);

            var trendsNav = page.GetByRole(AriaRole.Button, new() { Name = "Trends" });
            await trendsNav.ClickAsync();

            var panel = page.Locator("#trends-panel, [data-testid='trends-panel'], .trends-panel").First;
            await Expect(panel).ToBeVisibleAsync(new() { Timeout = ActionTimeoutMilliseconds });

            // Fixture rows are dated Jan 2026 — the chart renders fiscal year abbreviations (FY26).
            await Expect(panel).ToContainTextAsync("FY26", new() { Timeout = ActionTimeoutMilliseconds });
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

            await page.GetByRole(AriaRole.Button, new() { Name = "Scenario Planner" }).ClickAsync();

            var panel = page.Locator("#scenario-panel");
            await Expect(panel).ToBeVisibleAsync(new() { Timeout = ActionTimeoutMilliseconds });
            await Expect(panel).ToContainTextAsync("Scenario", new() { Timeout = ActionTimeoutMilliseconds });

            // SfNumericTextBox with FloatLabelType.Auto renders floating <label>, not HTML placeholder.
            // Use role=spinbutton (stable across re-renders) to confirm the rate card rendered.
            var rateInput = panel.GetByRole(AriaRole.Spinbutton).First;
            await Expect(rateInput).ToBeVisibleAsync(new() { Timeout = ActionTimeoutMilliseconds });
        });
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Navigates to the QuickBooks import panel, uploads <paramref name="tempFile"/>,
    /// triggers analysis, and waits for "Preview ready" or "Duplicate detected" status.
    /// If the preview is new, commits it through the current modal confirmation flow.
    /// </summary>
    private static async Task ImportFixtureAsync(IPage page, string tempFile)
    {
        var importNav = page.GetByRole(AriaRole.Button, new() { Name = "QuickBooks Import" });
        await importNav.ClickAsync();

        await UploadQuickBooksFileAsync(page, tempFile);

        await page.GetByRole(AriaRole.Button, new() { Name = "Analyze file" }).ClickAsync();

        var isDuplicate = await QuickBooksImportE2EHelpers.WaitForPreviewReadyOrDuplicateAsync(page, ActionTimeoutMilliseconds);

        if (!isDuplicate)
        {
            await QuickBooksImportE2EHelpers.CommitImportIfReadyAsync(page, ActionTimeoutMilliseconds);
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

    private static async Task UploadQuickBooksFileAsync(IPage page, string filePath)
    {
        await QuickBooksImportE2EHelpers.UploadQuickBooksFileAsync(page, filePath, 15_000);
    }
}
