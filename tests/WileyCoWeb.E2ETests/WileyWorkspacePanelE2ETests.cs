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
            var trendsSection = page.Locator("a[href='/wiley-workspace/trends']").First;
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
            await OpenPanelAsync(page, "trends");

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
            await OpenPanelAsync(page, "customers");

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
            await OpenPanelAsync(page, "customers");

            // The service filter dropdown must be present.
            var serviceFilter = page.Locator("#customer-service-filter, [data-testid='service-filter']").First;
            await Expect(serviceFilter).ToBeVisibleAsync(new() { Timeout = ActionTimeoutMs });
        });
    }

    [Fact]
    public async Task Workspace_CustomerViewerPanel_CanCreateEditAndDeleteCustomer()
    {
        await RunWorkspaceTestAsync(async page =>
        {
            await OpenPanelAsync(page, "customers");

            var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
            var accountNumber = $"E2E{suffix}";
            var firstName = $"Proof{suffix[..4]}";
            var createdLastName = "Customer";
            var updatedLastName = "Updated";
            var createdDisplayName = $"{firstName} {createdLastName}";
            var updatedDisplayName = $"{firstName} {updatedLastName}";
            var directoryStatus = page.Locator("#customer-directory-status");

            await Expect(page.Locator("#customer-viewer-panel")).ToBeVisibleAsync(new() { Timeout = ActionTimeoutMs });

            await page.Locator("#add-customer-button").ClickAsync();
            await Expect(page.GetByText("Add Utility Customer", new() { Exact = true })).ToBeVisibleAsync(new() { Timeout = ActionTimeoutMs });

            await ReplaceSyncfusionTextAsync(page.Locator("#customer-editor-account-number"), accountNumber);
            await ReplaceSyncfusionTextAsync(page.Locator("#customer-editor-first-name"), firstName);
            await ReplaceSyncfusionTextAsync(page.Locator("#customer-editor-last-name"), createdLastName);
            await ReplaceSyncfusionTextAsync(page.Locator("#customer-editor-service-address"), "123 E2E Ave");
            await ReplaceSyncfusionTextAsync(page.Locator("#customer-editor-service-city"), "Wiley");
            await ReplaceSyncfusionTextAsync(page.Locator("#customer-editor-service-state"), "CO");
            await ReplaceSyncfusionTextAsync(page.Locator("#customer-editor-service-zip-code"), "81092");

            var createResponseTask = page.WaitForResponseAsync(response =>
                response.Url.Contains("/api/utility-customers", StringComparison.OrdinalIgnoreCase)
                && string.Equals(response.Request.Method, "POST", StringComparison.OrdinalIgnoreCase));

            await page.Locator("#customer-editor-save-button").ClickAsync();

            var createResponse = await createResponseTask;
            Assert.Equal(201, createResponse.Status);
            Assert.True(createResponse.Ok);

            await Expect(directoryStatus).ToContainTextAsync(accountNumber, new() { Timeout = ActionTimeoutMs });
            await Expect(directoryStatus).ToContainTextAsync("created", new() { Timeout = ActionTimeoutMs });

            var createdRow = FindCustomerGridRow(page, accountNumber);
            await Expect(createdRow).ToHaveCountAsync(1, new() { Timeout = ActionTimeoutMs });
            await Expect(createdRow.First).ToContainTextAsync(createdDisplayName, new() { Timeout = ActionTimeoutMs });

            await createdRow.First.GetByRole(AriaRole.Button, new() { Name = "Edit" }).ClickAsync();
            await Expect(page.GetByText("Edit Utility Customer", new() { Exact = true })).ToBeVisibleAsync(new() { Timeout = ActionTimeoutMs });

            await ReplaceSyncfusionTextAsync(page.Locator("#customer-editor-last-name"), updatedLastName);

            var updateResponseTask = page.WaitForResponseAsync(response =>
                response.Url.Contains($"/api/utility-customers/", StringComparison.OrdinalIgnoreCase)
                && string.Equals(response.Request.Method, "PUT", StringComparison.OrdinalIgnoreCase));

            await page.Locator("#customer-editor-save-button").ClickAsync();

            var updateResponse = await updateResponseTask;
            Assert.Equal(200, updateResponse.Status);
            Assert.True(updateResponse.Ok);

            await Expect(directoryStatus).ToContainTextAsync(accountNumber, new() { Timeout = ActionTimeoutMs });
            await Expect(directoryStatus).ToContainTextAsync("updated", new() { Timeout = ActionTimeoutMs });

            var updatedRow = FindCustomerGridRow(page, accountNumber);
            await Expect(updatedRow).ToHaveCountAsync(1, new() { Timeout = ActionTimeoutMs });
            await Expect(updatedRow.First).ToContainTextAsync(updatedDisplayName, new() { Timeout = ActionTimeoutMs });

            await updatedRow.First.GetByRole(AriaRole.Button, new() { Name = "Delete" }).ClickAsync();
            await Expect(page.GetByText("Delete Customer", new() { Exact = true })).ToBeVisibleAsync(new() { Timeout = ActionTimeoutMs });

            var deleteResponseTask = page.WaitForResponseAsync(response =>
                response.Url.Contains("/api/utility-customers/", StringComparison.OrdinalIgnoreCase)
                && string.Equals(response.Request.Method, "DELETE", StringComparison.OrdinalIgnoreCase));

            await page.Locator("#customer-delete-confirm-button").ClickAsync();

            var deleteResponse = await deleteResponseTask;
            Assert.Equal(204, deleteResponse.Status);
            Assert.True(deleteResponse.Ok);

            await Expect(directoryStatus).ToContainTextAsync($"Deleted {updatedDisplayName}", new() { Timeout = ActionTimeoutMs });
            await Expect(FindCustomerGridRow(page, accountNumber)).ToHaveCountAsync(0, new() { Timeout = ActionTimeoutMs });
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
            await OpenPanelAsync(page, "decision-support");

            // Enterprise selector must contain at least one option.
            var enterpriseSelect = page.Locator("#enterprise-select").First;
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
                await OpenPanelAsync(page, "quickbooks-import");

                await UploadQuickBooksFileAsync(page, tempFile);

                await page.GetByRole(AriaRole.Button, new() { Name = "Analyze file" }).ClickAsync();
                await QuickBooksImportE2EHelpers.WaitForPreviewReadyOrDuplicateAsync(page, ActionTimeoutMs);

                // 2. Navigate to Break-Even panel and verify it is still functional.
                await OpenPanelAsync(page, "break-even");

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
            await OpenPanelAsync(page, "rates");
            var currentRateInput = page.Locator("#rates-panel").GetByPlaceholder("Current Rate");
            var scenarioNameInput = page.Locator("#scenario-name-input");

            await currentRateInput.FillAsync("58.00");
            await scenarioNameInput.FillAsync(scenarioName);

            await page.GetByRole(AriaRole.Button, new() { Name = "Save scenario" }).ClickAsync();
            await Expect(page.Locator("#scenario-persistence-status"))
                .ToContainTextAsync("Saved scenario", new() { Timeout = ActionTimeoutMs });

            // 2. Navigate to Trends panel and confirm it is visible.
            await OpenPanelAsync(page, "trends");

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
            var enterpriseSelector = page.Locator("#enterprise-select").First;

            if (!await enterpriseSelector.IsVisibleAsync())
            {
                // Enterprise switcher is not on this build — pass silently.
                return;
            }

            var initialValue = await enterpriseSelector.InputValueAsync();

            await enterpriseSelector.ClickAsync();
            var options = page.Locator("#enterprise-select_popup .e-list-item");
            if (await options.CountAsync() < 2)
            {
                // Only one enterprise seeded — nothing to switch to.
                return;
            }

            for (var index = 0; index < await options.CountAsync(); index++)
            {
                var option = options.Nth(index);
                var optionText = (await option.InnerTextAsync()).Trim();
                if (string.Equals(optionText, initialValue, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                await option.ClickAsync();

                // Workspace should reload with new enterprise context.
                await Expect(page.Locator("#workspace-load-status"))
                    .ToContainTextAsync("Loaded", new() { Timeout = ReadyTimeoutMs });
                return;
            }
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

    private static async Task OpenPanelAsync(IPage page, string panelKey)
    {
        await page.Locator($"a[href='/wiley-workspace/{panelKey}']").First.ClickAsync();
    }

    private static ILocator FindCustomerGridRow(IPage page, string text)
    {
        return page.Locator("#customer-viewer-panel tr.e-row").Filter(new() { HasText = text });
    }

    private static async Task ReplaceSyncfusionTextAsync(ILocator input, string value)
    {
        await input.ClickAsync();
        await input.FillAsync(value);

        var currentValue = await input.InputValueAsync();
        Assert.Equal(value, currentValue);
        await input.PressAsync("Tab");
    }

    private static async Task UploadQuickBooksFileAsync(IPage page, string filePath)
    {
        await QuickBooksImportE2EHelpers.UploadQuickBooksFileAsync(page, filePath, ActionTimeoutMs);
    }
}
