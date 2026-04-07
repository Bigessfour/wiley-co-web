using Microsoft.Playwright;

namespace WileyCoWeb.E2ETests
{
    public sealed class WorkspaceExportE2ETests
    {
        [Fact]
        public async Task Workspace_ExportButtons_TriggerExcelAndPdfDownloads()
        {
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

            var page = await context.NewPageAsync();
            page.Console += (_, message) => consoleMessages.Add($"{message.Type}: {message.Text}");
            page.PageError += (_, exception) => pageErrors.Add(exception);

            await page.GotoAsync($"{baseUrl.TrimEnd('/')}/wiley-workspace", new PageGotoOptions
            {
                WaitUntil = WaitUntilState.NetworkIdle
            });

            await page.WaitForTimeoutAsync(3000);

            var bodyText = await page.Locator("body").InnerTextAsync();
            Assert.True(
                bodyText.Contains("Utility Rate Study Workspace", StringComparison.Ordinal),
                $"Workspace route did not render as expected. Body text was: {bodyText}\nConsole: {string.Join(" | ", consoleMessages)}\nPageErrors: {string.Join(" | ", pageErrors)}");

            var customerExportButton = page.Locator("text=Export customers to Excel").First;
            await customerExportButton.WaitForAsync(new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = 15000
            });

            var customerDownload = await page.RunAndWaitForDownloadAsync(() => customerExportButton.ClickAsync());
            Assert.EndsWith(".xlsx", customerDownload.SuggestedFilename, StringComparison.OrdinalIgnoreCase);

            var scenarioExportButton = page.Locator("text=Export scenario to Excel").First;
            var scenarioDownload = await page.RunAndWaitForDownloadAsync(() => scenarioExportButton.ClickAsync());
            Assert.EndsWith(".xlsx", scenarioDownload.SuggestedFilename, StringComparison.OrdinalIgnoreCase);

            var pdfExportButton = page.Locator("text=Download PDF rate packet").First;
            var pdfDownload = await page.RunAndWaitForDownloadAsync(() => pdfExportButton.ClickAsync());
            Assert.EndsWith(".pdf", pdfDownload.SuggestedFilename, StringComparison.OrdinalIgnoreCase);
        }
    }
}