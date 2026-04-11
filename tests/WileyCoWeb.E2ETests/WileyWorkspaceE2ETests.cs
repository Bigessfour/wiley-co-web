using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace WileyCoWeb.E2ETests;

public sealed class WileyWorkspaceE2ETests
{
	private const int ReadyTimeoutMilliseconds = 90000;

	[Fact]
	public async Task Workspace_LoadsCoreShellSections()
	{
		await RunWorkspaceTestAsync(async page =>
		{
			await Expect(page.GetByText("Utility Rate Study Workspace", new() { Exact = true })).ToBeVisibleAsync();
			await Expect(page.GetByText("Break-Even Panel", new() { Exact = true })).ToBeVisibleAsync();
			await Expect(page.GetByText("Rates Panel", new() { Exact = true })).ToBeVisibleAsync();
			await Expect(page.GetByText("Scenario Planner", new() { Exact = true })).ToBeVisibleAsync();
			await Expect(page.GetByText("Customer Viewer", new() { Exact = true })).ToBeVisibleAsync();
			await Expect(page.GetByText("Trends & Projections", new() { Exact = true })).ToBeVisibleAsync();
			await Expect(page.GetByText("QuickBooks Import Panel", new() { Exact = true })).ToBeVisibleAsync();
			await Expect(page.GetByText("Decision Support", new() { Exact = true })).ToBeVisibleAsync();
		});
	}

	[Fact]
	public async Task Workspace_SaveRateSnapshot_UpdatesStatusText()
	{
		await RunWorkspaceTestAsync(async page =>
		{
			var currentRate = page.Locator("#rates-panel").GetByPlaceholder("Current Rate");
			await currentRate.FillAsync("63.50");

			await page.GetByRole(AriaRole.Button, new() { Name = "Save rate snapshot" }).ClickAsync();

			await Expect(page.Locator("#snapshot-save-status")).ToContainTextAsync("Saved", new() { Timeout = 30000 });
			await Expect(page.Locator("#snapshot-save-status")).ToContainTextAsync("snapshot", new() { Timeout = 30000 });
		});
	}

	[Fact]
	public async Task Workspace_SaveScenario_PersistsScenarioMessage()
	{
		await RunWorkspaceTestAsync(async page =>
		{
			var scenarioName = $"E2E Scenario {Guid.NewGuid():N}";
			var scenarioNameInput = page.Locator("#scenario-panel").GetByPlaceholder("Scenario name");

			await scenarioNameInput.FillAsync(scenarioName);
			await page.GetByRole(AriaRole.Button, new() { Name = "Save scenario" }).ClickAsync();

			await Expect(page.Locator("#scenario-persistence-status")).ToContainTextAsync("Saved scenario", new() { Timeout = 30000 });
			await Expect(page.Locator("#scenario-persistence-status")).ToContainTextAsync(scenarioName, new() { Timeout = 30000 });
		});
	}

	[Fact]
	public async Task Workspace_SaveBaseline_ReloadsPersistedWorkspaceValues()
	{
		await RunWorkspaceTestAsync(async page =>
		{
			var currentRateInput = page.Locator("#rates-panel").GetByPlaceholder("Current Rate");
			var totalCostsInput = page.Locator("#break-even-panel").GetByPlaceholder("Total Costs");
			var projectedVolumeInput = page.Locator("#break-even-panel").GetByPlaceholder("Projected Volume");

			await currentRateInput.FillAsync("71.25");
			await totalCostsInput.FillAsync("49250");
			await projectedVolumeInput.FillAsync("8400");

			await page.GetByRole(AriaRole.Button, new() { Name = "Save workspace baseline" }).ClickAsync();

			await Expect(page.Locator("#workspace-load-status")).ToContainTextAsync("Reloaded", new() { Timeout = 30000 });
			await Expect(page.Locator("#workspace-load-status")).ToContainTextAsync("after baseline save", new() { Timeout = 30000 });
			await Expect(page.Locator("#baseline-save-status")).ToContainTextAsync("Saved baseline values", new() { Timeout = 30000 });

			Assert.Equal("71.25", NormalizeNumericValue(await currentRateInput.InputValueAsync()));
			Assert.Equal("49250", NormalizeNumericValue(await totalCostsInput.InputValueAsync()));
			Assert.Equal("8400", NormalizeNumericValue(await projectedVolumeInput.InputValueAsync()));
		});
	}

	[Fact]
	public async Task Workspace_SaveAndApplyScenario_RestoresSavedWorkspaceValues()
	{
		await RunWorkspaceTestAsync(async page =>
		{
			var scenarioName = $"E2E Scenario {Guid.NewGuid():N}";
			var currentRateInput = page.Locator("#rates-panel").GetByPlaceholder("Current Rate");
			var totalCostsInput = page.Locator("#break-even-panel").GetByPlaceholder("Total Costs");
			var projectedVolumeInput = page.Locator("#break-even-panel").GetByPlaceholder("Projected Volume");
			var scenarioNameInput = page.Locator("#scenario-panel").GetByPlaceholder("Scenario name");

			await currentRateInput.FillAsync("88.25");
			await totalCostsInput.FillAsync("45000");
			await projectedVolumeInput.FillAsync("7000");
			await scenarioNameInput.FillAsync(scenarioName);

			await page.GetByRole(AriaRole.Button, new() { Name = "Save scenario" }).ClickAsync();

			await Expect(page.Locator("#scenario-persistence-status")).ToContainTextAsync("Saved scenario", new() { Timeout = 30000 });
			await Expect(page.Locator("#scenario-persistence-status")).ToContainTextAsync(scenarioName, new() { Timeout = 30000 });

			await currentRateInput.FillAsync("99.75");
			await totalCostsInput.FillAsync("52000");
			await projectedVolumeInput.FillAsync("8100");

			await page.GetByRole(AriaRole.Button, new() { Name = "Apply saved scenario" }).ClickAsync();

			await Expect(page.Locator("#scenario-persistence-status")).ToContainTextAsync("Applied saved scenario", new() { Timeout = 30000 });
			await Expect(page.Locator("#scenario-persistence-status")).ToContainTextAsync(scenarioName, new() { Timeout = 30000 });

			Assert.Equal("88.25", NormalizeNumericValue(await currentRateInput.InputValueAsync()));
			Assert.Equal("45000", NormalizeNumericValue(await totalCostsInput.InputValueAsync()));
			Assert.Equal("7000", NormalizeNumericValue(await projectedVolumeInput.InputValueAsync()));
		});
	}

	[Fact]
	public async Task Workspace_QuickBooksImportPanel_PreviewsUploadedFile()
	{
		var tempFile = Path.Combine(Path.GetTempPath(), $"quickbooks-e2e-{Guid.NewGuid():N}.csv");
		await File.WriteAllTextAsync(tempFile, CreateQuickBooksCsv());

		try
		{
			await RunWorkspaceTestAsync(async page =>
			{
				var browseButton = page.GetByRole(AriaRole.Button, new() { Name = "Choose QuickBooks file" });
				var fileChooser = await page.RunAndWaitForFileChooserAsync(() => browseButton.ClickAsync());
				await fileChooser.SetFilesAsync(tempFile);

				await page.GetByRole(AriaRole.Button, new() { Name = "Analyze file" }).ClickAsync();

				try
				{
					await Expect(page.Locator("#quickbooks-import-status-message")).ToContainTextAsync("Preview ready", new() { Timeout = 30000 });
				}
				catch (PlaywrightException)
				{
					await Expect(page.Locator("#quickbooks-import-status-message")).ToContainTextAsync("Duplicate detected", new() { Timeout = 30000 });
				}

				await Expect(page.Locator("#quickbooks-import-status-message")).ToContainTextAsync("quickbooks-ledger.csv", new() { Timeout = 30000 });
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

	[Fact]
	public async Task Workspace_JarvisChatPanel_SendsQuestion_AndShowsTranscript()
	{
		await RunWorkspaceTestAsync(async page =>
		{
			var question = $"What does the workspace know about FY 2026? {Guid.NewGuid():N}";
			var questionPrefix = question[..Math.Min(question.Length, 48)];
			var chatBox = page.Locator("#jarvis-question-input");

			await chatBox.FillAsync(question);
			await Expect(chatBox).ToHaveValueAsync(question, new() { Timeout = 30000 });
			await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Ask Jarvis" })).ToBeEnabledAsync();
			await page.GetByRole(AriaRole.Button, new() { Name = "Ask Jarvis" }).ClickAsync();

			await Expect(page.Locator("#jarvis-conversation-history")).ToContainTextAsync("You", new() { Timeout = 30000 });
			await Expect(page.Locator("#jarvis-conversation-history")).ToContainTextAsync(questionPrefix, new() { Timeout = 30000 });
			await Expect(page.Locator("#jarvis-chat-answer")).ToContainTextAsync("Conversation", new() { Timeout = 30000 });

			var transcriptText = await page.Locator("#jarvis-conversation-history").InnerTextAsync();
			Assert.Contains("Conversation history", transcriptText, StringComparison.Ordinal);
			Assert.Contains(questionPrefix, transcriptText, StringComparison.Ordinal);
		});
	}

	[Fact]
	public async Task Workspace_ExportButtons_TriggerExcelAndPdfDownloads()
	{
		await RunWorkspaceTestAsync(async page =>
		{
			var customerExportButton = page.Locator("text=Export customers to Excel").First;
			var customerDownload = await page.RunAndWaitForDownloadAsync(() => customerExportButton.ClickAsync());
			Assert.EndsWith(".xlsx", customerDownload.SuggestedFilename, StringComparison.OrdinalIgnoreCase);

			var scenarioExportButton = page.Locator("text=Export scenario to Excel").First;
			var scenarioDownload = await page.RunAndWaitForDownloadAsync(() => scenarioExportButton.ClickAsync());
			Assert.EndsWith(".xlsx", scenarioDownload.SuggestedFilename, StringComparison.OrdinalIgnoreCase);

			var pdfExportButton = page.Locator("text=Download PDF rate packet").First;
			var pdfDownload = await page.RunAndWaitForDownloadAsync(() => pdfExportButton.ClickAsync());
			Assert.EndsWith(".pdf", pdfDownload.SuggestedFilename, StringComparison.OrdinalIgnoreCase);
		});
	}

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
			await Expect(page.Locator("#workspace-load-status")).ToContainTextAsync("Workspace ready.", new() { Timeout = ReadyTimeoutMilliseconds });

			await testBody(page);
		}
		catch (Exception ex)
		{
			var diagnostics = string.Join(Environment.NewLine, [
				"Console messages:",
				..consoleMessages.Select(message => $"- {message}"),
				"Page errors:",
				..pageErrors.Select(error => $"- {error}")
			]);

			throw new InvalidOperationException($"E2E failure at {page.Url}{Environment.NewLine}{diagnostics}", ex);
		}
	}

	private static string CreateQuickBooksCsv()
	{
		return "Date,Type,Num,Name,Memo,Account,Split,Amount,Balance,Clr\n" +
			   "01/01/2026,Invoice,1001,Town of Wiley,Water Billing,Water Revenue,Accounts Receivable,125.00,125.00,C\n" +
			   "01/02/2026,Payment,1002,Town of Wiley,Payment Received,Accounts Receivable,Water Revenue,-125.00,0.00,C\n";
	}

	private static string NormalizeNumericValue(string value)
	{
		return new string(value.Where(character => char.IsDigit(character) || character is '.' or '-').ToArray());
	}
}