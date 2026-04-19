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
			await Expect(page.Locator("a[href='/wiley-workspace/break-even']").First).ToContainTextAsync("Break-Even");
			await Expect(page.Locator("a[href='/wiley-workspace/rates']").First).ToContainTextAsync("Rates");
			await Expect(page.Locator("a[href='/wiley-workspace/scenario']").First).ToContainTextAsync("Scenario Planner");
			await Expect(page.Locator("a[href='/wiley-workspace/customers']").First).ToContainTextAsync("Customer Viewer");
			await Expect(page.Locator("a[href='/wiley-workspace/trends']").First).ToContainTextAsync("Trends");
			await Expect(page.Locator("a[href='/wiley-workspace/quickbooks-import']").First).ToContainTextAsync("QuickBooks Import");
			await Expect(page.Locator("a[href='/wiley-workspace/decision-support']").First).ToContainTextAsync("Decision Support");
		});
	}

	[Fact]
	public async Task Workspace_SaveRateSnapshot_UpdatesStatusText()
	{
		await RunWorkspaceTestAsync(async page =>
		{
			await OpenPanelAsync(page, "rates");

			var currentRate = page.Locator("#current-rate-input input");
			await E2ETestHelpers.EnterNumericValueAsync(currentRate, "63.50");

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
			var scenarioNameInput = page.Locator("#scenario-name-input");

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
			await OpenPanelAsync(page, "rates");
			var currentRateInput = page.Locator("#current-rate-input input");
			await E2ETestHelpers.EnterNumericValueAsync(currentRateInput, "71.25");

			await OpenPanelAsync(page, "break-even");
			var breakEvenInputs = page.Locator("#break-even-input-row input");
			var totalCostsInput = breakEvenInputs.Nth(0);
			var projectedVolumeInput = breakEvenInputs.Nth(1);

			await E2ETestHelpers.EnterNumericValueAsync(totalCostsInput, "49250");
			await E2ETestHelpers.EnterNumericValueAsync(projectedVolumeInput, "8400");

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
	public async Task Workspace_Persistence_RestoresEditedValues_AfterBrowserReload()
	{
		await RunWorkspaceTestAsync(async page =>
		{
			await OpenPanelAsync(page, "rates");
			var currentRateInput = page.Locator("#current-rate-input input");
			await E2ETestHelpers.EnterNumericValueAsync(currentRateInput, "64.5");

			await OpenPanelAsync(page, "break-even");
			var breakEvenInputs = page.Locator("#break-even-input-row input");
			var totalCostsInput = breakEvenInputs.Nth(0);
			var projectedVolumeInput = breakEvenInputs.Nth(1);
			await E2ETestHelpers.EnterNumericValueAsync(totalCostsInput, "40250");
			await E2ETestHelpers.EnterNumericValueAsync(projectedVolumeInput, "650");

			await page.WaitForFunctionAsync(@"() => {
				const stored = globalThis.localStorage.getItem('wiley.workspace.state.v1') || '';
				return stored.includes('40250') && stored.includes('650') && stored.includes('64.5');
			}", null, new() { Timeout = 30000 });

			await page.ReloadAsync(new PageReloadOptions
			{
				WaitUntil = WaitUntilState.DOMContentLoaded
			});

			await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
			await Expect(page.Locator("#workspace-load-status")).ToContainTextAsync("Workspace ready.", new() { Timeout = 30000 });
			await Expect(page.Locator("#workspace-status-card")).ToContainTextAsync("browser storage", new() { Timeout = 30000 });

			Assert.Equal("40250", NormalizeNumericValue(await totalCostsInput.InputValueAsync()));
			Assert.Equal("650", NormalizeNumericValue(await projectedVolumeInput.InputValueAsync()));

			await OpenPanelAsync(page, "rates");
			Assert.Equal("64.5", NormalizeNumericValue(await currentRateInput.InputValueAsync()));
		});
	}

	[Fact]
	public async Task Workspace_SaveAndApplyScenario_RestoresSavedWorkspaceValues()
	{
		await RunWorkspaceTestAsync(async page =>
		{
			var scenarioName = $"E2E Scenario {Guid.NewGuid():N}";
			await OpenPanelAsync(page, "rates");
			var currentRateInput = page.Locator("#current-rate-input input");
			await E2ETestHelpers.EnterNumericValueAsync(currentRateInput, "88.25");

			await OpenPanelAsync(page, "break-even");
			var breakEvenInputs = page.Locator("#break-even-input-row input");
			var totalCostsInput = breakEvenInputs.Nth(0);
			var projectedVolumeInput = breakEvenInputs.Nth(1);
			var scenarioNameInput = page.Locator("#scenario-name-input");

			await E2ETestHelpers.EnterNumericValueAsync(totalCostsInput, "45000");
			await E2ETestHelpers.EnterNumericValueAsync(projectedVolumeInput, "7000");
			await scenarioNameInput.FillAsync(scenarioName);

			await page.GetByRole(AriaRole.Button, new() { Name = "Save scenario" }).ClickAsync();

			await Expect(page.Locator("#scenario-persistence-status")).ToContainTextAsync("Saved scenario", new() { Timeout = 30000 });
			await Expect(page.Locator("#scenario-persistence-status")).ToContainTextAsync(scenarioName, new() { Timeout = 30000 });

			await OpenPanelAsync(page, "rates");
			await currentRateInput.FillAsync("99.75");

			await OpenPanelAsync(page, "break-even");
			await totalCostsInput.FillAsync("52000");
			await projectedVolumeInput.FillAsync("8100");

			await page.GetByRole(AriaRole.Button, new() { Name = "Apply saved scenario" }).ClickAsync();

			await Expect(page.Locator("#scenario-persistence-status")).ToContainTextAsync("Applied saved scenario", new() { Timeout = 30000 });
			await Expect(page.Locator("#scenario-persistence-status")).ToContainTextAsync(scenarioName, new() { Timeout = 30000 });

			Assert.Equal("45000", NormalizeNumericValue(await totalCostsInput.InputValueAsync()));
			Assert.Equal("7000", NormalizeNumericValue(await projectedVolumeInput.InputValueAsync()));

			await OpenPanelAsync(page, "rates");
			Assert.Equal("88.25", NormalizeNumericValue(await currentRateInput.InputValueAsync()));
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
				await OpenPanelAsync(page, "quickbooks-import");

				await UploadQuickBooksFileAsync(page, tempFile);

				await page.GetByRole(AriaRole.Button, new() { Name = "Analyze file" }).ClickAsync();
				await QuickBooksImportE2EHelpers.WaitForPreviewReadyOrDuplicateAsync(page, 30000);
				await Expect(page.Locator("#quickbooks-assistant-context-summary")).ToContainTextAsync(Path.GetFileName(tempFile), new() { Timeout = 30000 });
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
	public async Task Workspace_QuickBooksImportPanel_AssistantAnswersQuestion_ForLoadedPreview()
	{
		var tempFile = Path.Combine(Path.GetTempPath(), $"quickbooks-assistant-e2e-{Guid.NewGuid():N}.csv");
		await File.WriteAllTextAsync(tempFile, CreateQuickBooksCsv());

		try
		{
			await RunWorkspaceTestAsync(async page =>
			{
				await OpenPanelAsync(page, "quickbooks-import");

				await UploadQuickBooksFileAsync(page, tempFile);

				await page.GetByRole(AriaRole.Button, new() { Name = "Analyze file" }).ClickAsync();
				await QuickBooksImportE2EHelpers.WaitForPreviewReadyOrDuplicateAsync(page, 30000);

				var assistantQuestionInput = page.Locator("#quickbooks-assistant-question");
				var assistantAnswer = page.Locator("#quickbooks-assistant-answer");
				var initialAnswer = (await assistantAnswer.InnerTextAsync()).Trim();
				const string question = "Why would this file be blocked as a duplicate?";

				page.Request += (_, request) =>
				{
					if (request.Url.Contains("/api/imports/quickbooks/assistant", StringComparison.OrdinalIgnoreCase))
					{
						Console.WriteLine($"[AssistantTest] REQUEST {request.Method} {request.Url}");
					}
				};

				page.Response += (_, response) =>
				{
					if (response.Url.Contains("/api/imports/quickbooks/assistant", StringComparison.OrdinalIgnoreCase))
					{
						Console.WriteLine($"[AssistantTest] RESPONSE {response.Status} {response.Url}");
					}
				};

				page.RequestFailed += (_, request) =>
				{
					if (request.Url.Contains("/api/imports/quickbooks/assistant", StringComparison.OrdinalIgnoreCase))
					{
						Console.WriteLine($"[AssistantTest] REQUEST FAILED {request.Method} {request.Url}");
					}
				};

				// SfTextBox with Multiline=true places the ID on the native <textarea> itself.
				// FillAsync sets value without firing JS input events Syncfusion listens to.
				// ClickAsync + PressSequentiallyAsync fires real keystrokes → input events →
				// Syncfusion ValueChange → Blazor @bind-Value update → button becomes enabled.
				Console.WriteLine("[AssistantTest] Clicking textarea and typing question...");
				await assistantQuestionInput.ClickAsync();
				await assistantQuestionInput.PressSequentiallyAsync(question, new() { Delay = 20 });
				// Syncfusion SfTextBox fires ValueChange (which backs @bind-Value) on blur, not input.
				// Tab press blurs the textarea, triggering ValueChange → AssistantQuestion set.
				Console.WriteLine("[AssistantTest] Pressing Tab to blur → trigger ValueChange...");
				await assistantQuestionInput.PressAsync("Tab");
				var askButton = page.GetByRole(AriaRole.Button, new() { Name = "Ask assistant" });
				Console.WriteLine("[AssistantTest] Waiting for Ask button to be enabled (up to 15s)...");
				await Expect(askButton).ToBeEnabledAsync(new() { Timeout = 15000 });
				Console.WriteLine("[AssistantTest] Ask button is enabled — clicking...");
				await askButton.ClickAsync();

				// Confirm the click actually fired AskAssistantAsync by checking button goes disabled within 3s.
				var isAskingStarted = false;
				try
				{
					await Expect(askButton).ToBeDisabledAsync(new() { Timeout = 3000 });
					isAskingStarted = true;
					Console.WriteLine("[AssistantTest] Button is now DISABLED — AskAssistantAsync is running.");
				}
				catch
				{
					Console.WriteLine("[AssistantTest] WARNING: Button did NOT become disabled after click — AskAssistantAsync may not have fired.");
				}

				Console.WriteLine("[AssistantTest] Checking context summary contains filename...");
				await Expect(page.Locator("#quickbooks-assistant-context-summary")).ToContainTextAsync(Path.GetFileName(tempFile), new() { Timeout = 30000 });
				Console.WriteLine("[AssistantTest] Context summary OK. Waiting for AI response (polling every 10s, max 300s)...");

				// Poll manually so we can log progress; button re-enables only after AskAssistantAsync finally runs.
				const int pollIntervalMs = 10000;
				const int maxWaitMs = 300000;
				var sw = System.Diagnostics.Stopwatch.StartNew();
				string currentAnswer;
				bool buttonEnabled;
				do
				{
					await Task.Delay(pollIntervalMs);
					currentAnswer = (await assistantAnswer.InnerTextAsync()).Trim();
					buttonEnabled = await askButton.IsEnabledAsync();
					Console.WriteLine($"[AssistantTest] {sw.Elapsed:mm\\:ss} elapsed — button enabled={buttonEnabled}, answer changed={currentAnswer != initialAnswer}, answer preview='{currentAnswer[..Math.Min(80, currentAnswer.Length)]}'");
				}
				while (!buttonEnabled && sw.ElapsedMilliseconds < maxWaitMs);
				sw.Stop();

				if (!buttonEnabled)
				{
					throw new InvalidOperationException($"[AssistantTest] AI response did not complete within {maxWaitMs / 1000}s. isAskingStarted={isAskingStarted}. Last answer: '{currentAnswer}'");
				}

				Console.WriteLine($"[AssistantTest] Button re-enabled after {sw.Elapsed:mm\\:ss}. Reading final answer...");
				var updatedAnswer = (await assistantAnswer.InnerTextAsync()).Trim();
				Assert.NotEqual(initialAnswer, updatedAnswer);
				Assert.False(string.IsNullOrWhiteSpace(updatedAnswer));
				Assert.DoesNotContain("timedout", updatedAnswer, StringComparison.OrdinalIgnoreCase);
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
			await OpenPanelAsync(page, "decision-support");

			var question = $"What does the workspace know about FY 2026? {Guid.NewGuid():N}";
			var questionPrefix = question[..Math.Min(question.Length, 48)];
			var chatBox = page.Locator("#jarvis-question-input");
			var initialAnswer = (await page.Locator("#jarvis-chat-answer").InnerTextAsync()).Trim();

			await chatBox.FillAsync(question);
			await Expect(chatBox).ToHaveValueAsync(question, new() { Timeout = 30000 });
			await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Ask Jarvis" })).ToBeEnabledAsync();
			await page.GetByRole(AriaRole.Button, new() { Name = "Ask Jarvis" }).ClickAsync();

			await Expect(page.Locator("#jarvis-conversation-history")).ToContainTextAsync("You", new() { Timeout = 30000 });
			await Expect(page.Locator("#jarvis-conversation-history")).ToContainTextAsync(questionPrefix, new() { Timeout = 30000 });
			await page.WaitForFunctionAsync(
				"([selector, initialText]) => { const element = document.querySelector(selector); return !!element && element.innerText.trim() !== initialText; }",
				new object[] { "#jarvis-chat-answer", initialAnswer },
				new() { Timeout = 30000 });

			var updatedAnswer = (await page.Locator("#jarvis-chat-answer").InnerTextAsync()).Trim();
			Assert.NotEqual(initialAnswer, updatedAnswer);
			Assert.False(string.IsNullOrWhiteSpace(updatedAnswer));

			var transcriptText = await page.Locator("#jarvis-conversation-history").InnerTextAsync();
			Assert.Contains("Conversation history", transcriptText, StringComparison.OrdinalIgnoreCase);
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

	private static async Task OpenPanelAsync(IPage page, string panelKey)
	{
		await page.Locator($"a[href='/wiley-workspace/{panelKey}']").First.ClickAsync();
	}

	private static async Task UploadQuickBooksFileAsync(IPage page, string filePath)
	{
		await QuickBooksImportE2EHelpers.UploadQuickBooksFileAsync(page, filePath, 30000);
	}

	private static string NormalizeNumericValue(string value)
	{
		return new string(value.Where(character => char.IsDigit(character) || character is '.' or '-').ToArray());
	}
}