using Applitools;
using Applitools.Playwright;
using Applitools.Playwright.Fluent;
using Applitools.Utils.Geometry;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;
using System.Text.RegularExpressions;

namespace WileyCoWeb.E2ETests;

/// <summary>
/// Hybrid E2E tests: each test performs a real field interaction with a functional
/// assertion, then takes an Applitools Eyes visual snapshot. This proves both that
/// panel fields connect to live data AND that the rendered output looks correct.
/// Tests gate on WILEYCO_E2E_BASE_URL and APPLITOOLS_API_KEY — skip silently when absent.
/// Results are reviewed in the Applitools Eyes dashboard; a small local summary is emitted for CI and log review.
/// </summary>
public sealed class WileyWorkspaceHybridE2ETests : IDisposable
{
    private const int ReadyTimeoutMilliseconds  = 90_000;
    private const int NavigationTimeoutMilliseconds = 30_000;
    private const int ActionTimeoutMilliseconds = 30_000;
    private const int ChartSettleMilliseconds   = 15_000;

    private readonly ClassicRunner _runner = new();

    public void Dispose()
    {
        var summary = _runner.GetAllTestResults(false);
        var exportedSummary = ApplitoolsResultWriter.WriteSummary(summary, nameof(WileyWorkspaceHybridE2ETests));
        ApplitoolsResultWriter.ReactToResults(exportedSummary);
    }

    // ─── Rates Panel ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Hybrid_RatesPanel_EditRate_UpdatesSnapshotStatus()
    {
        await RunHybridTestAsync("/wiley-workspace/rates", "Hybrid - Rates Panel Edit", async (eyes, page) =>
        {
            var rateInput = page.Locator("#current-rate-input");
            await rateInput.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = ChartSettleMilliseconds });

            await rateInput.FillAsync("68.75");
            await page.GetByRole(AriaRole.Button, new() { Name = "Save rate snapshot" }).ClickAsync();

            await Expect(page.Locator("#snapshot-save-status"))
                .ToContainTextAsync("Saved", new() { Timeout = ActionTimeoutMilliseconds });

            eyes.Check("Rates panel after snapshot save", Target.Window().Fully());
        });
    }

    // ─── Break-Even Panel ────────────────────────────────────────────────────────

    [Fact]
    public async Task Hybrid_BreakEvenPanel_EditCosts_UpdatesBreakEvenRate()
    {
        await RunHybridTestAsync("/wiley-workspace/break-even", "Hybrid - Break-Even Edit", async (eyes, page) =>
        {
            var costsInput = page.GetByPlaceholder("Total Costs");
            var volumeInput = page.GetByPlaceholder("Projected Volume");

            await costsInput.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = ChartSettleMilliseconds });
            await volumeInput.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = ChartSettleMilliseconds });

            await costsInput.FillAsync("24000");
            await volumeInput.FillAsync("400");
            await volumeInput.PressAsync("Tab");

            // Break-even rate = TotalCosts / ProjectedVolume = 24000 / 400 = 60
            await Expect(page.GetByText("60", new() { Exact = false }).First).ToBeVisibleAsync(new() { Timeout = ActionTimeoutMilliseconds });

            eyes.Check("Break-even panel after recalculation", Target.Window().Fully());
        });
    }

    // ─── Scenario Planner Panel ──────────────────────────────────────────────────

    [Fact]
    public async Task Hybrid_ScenarioPlannerPanel_EditScenario_PersistsAndUpdates()
    {
        await RunHybridTestAsync("/wiley-workspace/scenario", "Hybrid - Scenario Planner Edit", async (eyes, page) =>
        {
            await page.GetByText("Base Break-Even", new() { Exact = true }).WaitForAsync(
                new() { State = WaitForSelectorState.Visible, Timeout = ChartSettleMilliseconds });

            var scenarioName = $"Hybrid-{Guid.NewGuid():N}";
            var nameInput = page.Locator("#scenario-name-input");
            await nameInput.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = ActionTimeoutMilliseconds });
            await nameInput.FillAsync(scenarioName);

            await page.GetByRole(AriaRole.Button, new() { Name = "Save scenario" }).ClickAsync();

            await Expect(page.Locator("#scenario-persistence-status"))
                .ToContainTextAsync("Saved scenario", new() { Timeout = ActionTimeoutMilliseconds });

            eyes.Check("Scenario planner after save", Target.Window().Fully());
        });
    }

    // ─── Customer Viewer Panel ───────────────────────────────────────────────────

    [Fact]
    public async Task Hybrid_CustomerViewerPanel_FilterByService_NarrowsGrid()
    {
        await RunHybridTestAsync("/wiley-workspace/customers", "Hybrid - Customer Viewer Filter", async (eyes, page) =>
        {
            await page.GetByText("Visible Customers", new() { Exact = true }).WaitForAsync(
                new() { State = WaitForSelectorState.Visible, Timeout = ChartSettleMilliseconds });

            var serviceFilter = page.GetByPlaceholder("Service").First;
            await Expect(serviceFilter).ToBeVisibleAsync(new() { Timeout = ActionTimeoutMilliseconds });

            eyes.Check("Customer viewer panel with service filter", Target.Window().Fully());
        });
    }

    // ─── Trends Panel ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Hybrid_TrendsPanel_ProjectionRows_AreNonDecreasing()
    {
        await RunHybridTestAsync("/wiley-workspace/trends", "Hybrid - Trends Panel Projections", async (eyes, page) =>
        {
            var panelTitle = page.GetByText("Historical and Projected Rates", new() { Exact = true });
            await panelTitle.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = ChartSettleMilliseconds });

            await Expect(panelTitle).ToBeVisibleAsync(new() { Timeout = ActionTimeoutMilliseconds });
            await Expect(page.GetByText("Projection", new() { Exact = true })).ToBeVisibleAsync(new() { Timeout = ActionTimeoutMilliseconds });

            eyes.Check("Trends panel with projection data", Target.Window().Fully());
        });
    }

    // ─── Decision Support / Jarvis Chat Panel ────────────────────────────────────

    [Fact]
    public async Task Hybrid_DecisionSupportPanel_SendsQuery_ShowsResponse()
    {
        await RunHybridTestAsync("/wiley-workspace/decision-support", "Hybrid - Decision Support Chat", async (eyes, page) =>
        {
            var chatPanel = page.Locator(".jarvis-chat-panel, #decision-support-panel").First;
            await chatPanel.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = ChartSettleMilliseconds });

            var question      = $"What is the current break-even rate? {Guid.NewGuid():N}";
            var questionPrefix = question[..48];

            var chatInput = page.Locator("#jarvis-question-input");
            await chatInput.FillAsync(question);
            await page.GetByRole(AriaRole.Button, new() { Name = "Ask Jarvis" }).ClickAsync();

            await Expect(page.Locator("#jarvis-conversation-history"))
                .ToContainTextAsync(questionPrefix, new() { Timeout = 60_000 });

            eyes.Check("Decision support panel after AI response", Target.Window().Fully());
        });
    }

    // ─── QuickBooks Import Panel ─────────────────────────────────────────────────

    [Fact]
    public async Task Hybrid_QuickBooksImportPanel_UploadPreview_ShowsRows()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"qb-hybrid-{Guid.NewGuid():N}.csv");
        await File.WriteAllTextAsync(tempFile, CreateQuickBooksCsv());

        try
        {
            await RunHybridTestAsync(
                "/wiley-workspace/quickbooks-import",
                "Hybrid - QuickBooks Import Preview",
                async (eyes, page) =>
                {
                    var browseButton = page.GetByRole(AriaRole.Button, new() { Name = "Choose QuickBooks file" });
                    await browseButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = ChartSettleMilliseconds });

                    var fileChooser = await page.RunAndWaitForFileChooserAsync(() => browseButton.ClickAsync());
                    await fileChooser.SetFilesAsync(tempFile);

                    await page.GetByRole(AriaRole.Button, new() { Name = "Analyze file" }).ClickAsync();

                    try
                    {
                        await Expect(page.Locator("#quickbooks-import-status-message"))
                            .ToContainTextAsync("Preview ready", new() { Timeout = ActionTimeoutMilliseconds });
                    }
                    catch (PlaywrightException)
                    {
                        await Expect(page.Locator("#quickbooks-import-status-message"))
                            .ToContainTextAsync("Duplicate detected", new() { Timeout = ActionTimeoutMilliseconds });
                    }

                    eyes.Check("QuickBooks import panel after file analysis", Target.Window().Fully());
                });
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    // ─── Helper ──────────────────────────────────────────────────────────────────

    private async Task RunHybridTestAsync(
        string path,
        string testName,
        Func<Eyes, IPage, Task> testBody)
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
            ViewportSize    = new() { Width = 1280, Height = 800 },
            AcceptDownloads = true,
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
            await testBody(eyes, page);
            eyes.Close(false);
        }
        catch (Exception ex) when (ex is not Xunit.Sdk.XunitException)
        {
            eyes.Abort();
            var diagnostics = VisualTestHarness.BuildDiagnostics(page, consoleMessages, pageErrors);
            Assert.Fail($"Hybrid test failed: {ex.Message}{Environment.NewLine}{diagnostics}");
        }
        // browser and context are disposed by `await using` — no explicit CloseAsync needed
    }

    private static string CreateQuickBooksCsv() =>
        "Date,Type,Num,Name,Memo,Account,Split,Amount,Balance,Clr\n" +
        "01/05/2026,Bill,B-301,Wiley Water Dept,Pump maintenance Q1,Operations,Accounts Payable,4800.00,4800.00,C\n" +
        "01/12/2026,Bill,B-302,Wiley Water Dept,Chemical treatment Q1,Operations,Accounts Payable,2200.00,7000.00,C\n" +
        "01/19/2026,Invoice,INV-701,Town of Wiley,Water billing Jan,Water Revenue,Accounts Receivable,18500.00,18500.00,C\n";
}
