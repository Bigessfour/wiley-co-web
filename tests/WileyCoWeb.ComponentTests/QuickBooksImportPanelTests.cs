using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Bunit;
using Bunit.JSInterop;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Syncfusion.Blazor;
using WileyCoWeb.Components;
using WileyCoWeb.Contracts;
using WileyCoWeb.Services;
using WileyCoWeb.State;

namespace WileyCoWeb.ComponentTests;

public sealed class QuickBooksImportPanelTests : TestContext
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
	{
		PropertyNameCaseInsensitive = true
	};

	[Fact]
	public void RendersQuickBooksImportWorkflowChrome()
	{
		var state = new WorkspaceState();
		var service = CreateImportService(_ => CreateJsonResponse(new QuickBooksImportPreviewResponse(
			"quickbooks-ledger.csv",
			"file-hash",
			WorkspaceTestData.WaterUtility,
			WorkspaceTestData.WaterFiscalYear,
			0,
			0,
			false,
			"Preview loaded for quickbooks-ledger.csv.",
			[])));

		Services.AddSingleton(state);
		Services.AddSingleton(service);
		Services.AddSyncfusionBlazor();

		SetRendererInfo(new RendererInfo("Server", true));
		JSInterop.Mode = JSRuntimeMode.Loose;

		var cut = RenderComponent<QuickBooksImportPanel>(parameters => parameters
			.Add(panel => panel.WorkspaceState, state));

		Assert.Contains("QuickBooks Import", cut.Markup);
		Assert.Contains("Choose QuickBooks file", cut.Markup);
		Assert.Contains("Commit import", cut.Markup);
		Assert.Contains("Ask assistant", cut.Markup);
		Assert.Contains("No file selected", cut.Markup);
		Assert.Single(cut.FindComponents<Syncfusion.Blazor.Navigations.SfTab>());
		Assert.Equal(3, cut.FindComponents<Syncfusion.Blazor.Navigations.TabItem>().Count);
	}

	[Fact]
	public async Task LoadPreviewAsync_PopulatesPreviewRows_AndUpdatesAssistantContext()
	{
		var state = new WorkspaceState();
		var service = CreateImportService(_ => CreateJsonResponse(CreatePreviewResponse(isDuplicate: false)));

		Services.AddSingleton(state);
		Services.AddSingleton(service);
		Services.AddSyncfusionBlazor();

		SetRendererInfo(new RendererInfo("Server", true));
		JSInterop.Mode = JSRuntimeMode.Loose;

		var cut = RenderComponent<QuickBooksImportPanel>(parameters => parameters
			.Add(panel => panel.WorkspaceState, state));

		SetPrivateField(cut.Instance, "SelectedFileBytes", new byte[] { 1, 2, 3, 4 });
		SetPrivateField(cut.Instance, "SelectedFileName", "quickbooks-ledger.csv");

		await InvokePrivateAsync(cut, "LoadPreviewAsync");

		var previewResponse = GetPrivateField<QuickBooksImportPreviewResponse?>(cut.Instance, "PreviewResponse");
		var previewRows = GetPrivateField<List<QuickBooksImportPreviewRow>>(cut.Instance, "PreviewRows");

		Assert.NotNull(previewResponse);
		Assert.Equal("Preview ready", GetPrivateField<string>(cut.Instance, "StatusHeadline"));
		Assert.Equal("Preview loaded for quickbooks-ledger.csv.", GetPrivateField<string>(cut.Instance, "StatusMessage"));
		Assert.Equal(1, GetPrivateField<int>(cut.Instance, "ActiveStep"));
		Assert.Equal(75, GetPrivateField<int>(cut.Instance, "ImportProgress"));
		Assert.Equal(2, previewRows.Count);
		Assert.Equal(WorkspaceTestData.QuickBooksAssistantContextSummary, GetPrivateField<string>(cut.Instance, "AssistantContextSummary"));
		Assert.False(previewResponse!.IsDuplicate);
	}

	[Fact]
	public async Task LoadPreviewAsync_WhenDuplicateIsDetected_PreventsCommit()
	{
		var state = new WorkspaceState();
		var service = CreateImportService(_ => CreateJsonResponse(CreatePreviewResponse(isDuplicate: true)));

		Services.AddSingleton(state);
		Services.AddSingleton(service);
		Services.AddSyncfusionBlazor();

		SetRendererInfo(new RendererInfo("Server", true));
		JSInterop.Mode = JSRuntimeMode.Loose;

		var cut = RenderComponent<QuickBooksImportPanel>(parameters => parameters
			.Add(panel => panel.WorkspaceState, state));

		SetPrivateField(cut.Instance, "SelectedFileBytes", new byte[] { 9, 8, 7, 6 });
		SetPrivateField(cut.Instance, "SelectedFileName", "duplicate-ledger.csv");

		await InvokePrivateAsync(cut, "LoadPreviewAsync");

		var previewResponse = GetPrivateField<QuickBooksImportPreviewResponse?>(cut.Instance, "PreviewResponse");

		Assert.Equal("Duplicate detected", GetPrivateField<string>(cut.Instance, "StatusHeadline"));
		Assert.Equal(55, GetPrivateField<int>(cut.Instance, "ImportProgress"));
		Assert.Equal(2, GetPrivateField<List<QuickBooksImportPreviewRow>>(cut.Instance, "PreviewRows").Count);
		Assert.NotNull(previewResponse);
		Assert.True(previewResponse!.IsDuplicate);
		Assert.Contains("file duplicate = True", GetPrivateField<string>(cut.Instance, "AssistantContextSummary"));
	}

	[Fact]
	public async Task LoadPreviewAsync_RejectsUnsupportedFileTypes()
	{
		var state = new WorkspaceState();
		var service = CreateImportService(_ => throw new InvalidOperationException("Preview API should not be called for unsupported files."));

		Services.AddSingleton(state);
		Services.AddSingleton(service);
		Services.AddSyncfusionBlazor();

		SetRendererInfo(new RendererInfo("Server", true));
		JSInterop.Mode = JSRuntimeMode.Loose;

		var cut = RenderComponent<QuickBooksImportPanel>(parameters => parameters
			.Add(panel => panel.WorkspaceState, state));

		SetPrivateField(cut.Instance, "SelectedFileBytes", new byte[] { 9, 9, 9 });
		SetPrivateField(cut.Instance, "SelectedFileName", "not-a-quickbooks-file.txt");

		await InvokePrivateAsync(cut, "LoadPreviewAsync");

		Assert.Equal("Unsupported file type", GetPrivateField<string>(cut.Instance, "StatusHeadline"));
		Assert.Equal("QuickBooks imports support CSV or Excel files only.", GetPrivateField<string>(cut.Instance, "StatusMessage"));
		Assert.Empty(GetPrivateField<List<QuickBooksImportPreviewRow>>(cut.Instance, "PreviewRows"));
		Assert.Null(GetPrivateField<QuickBooksImportPreviewResponse?>(cut.Instance, "PreviewResponse"));
		Assert.Equal(0, GetPrivateField<int>(cut.Instance, "ActiveStep"));
		Assert.Equal(0, GetPrivateField<int>(cut.Instance, "ImportProgress"));
	}

	[Fact]
	public async Task LoadPreviewAsync_ReportsApiFailureDetails_FromProblemResponse()
	{
		var state = new WorkspaceState();
		var service = CreateImportService(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
		{
			Content = new StringContent("{\"title\":\"Preview unavailable\",\"detail\":\"preview service unavailable\"}", Encoding.UTF8, "application/json")
		});

		Services.AddSingleton(state);
		Services.AddSingleton(service);
		Services.AddSyncfusionBlazor();

		SetRendererInfo(new RendererInfo("Server", true));
		JSInterop.Mode = JSRuntimeMode.Loose;

		var cut = RenderComponent<QuickBooksImportPanel>(parameters => parameters
			.Add(panel => panel.WorkspaceState, state));

		SetPrivateField(cut.Instance, "SelectedFileBytes", new byte[] { 1, 2, 3, 4 });
		SetPrivateField(cut.Instance, "SelectedFileName", "quickbooks-ledger.csv");

		await InvokePrivateAsync(cut, "LoadPreviewAsync");

		Assert.Equal("Preview failed", GetPrivateField<string>(cut.Instance, "StatusHeadline"));
		Assert.Equal("QuickBooks preview failed with status 500: preview service unavailable", GetPrivateField<string>(cut.Instance, "StatusMessage"));
		Assert.Empty(GetPrivateField<List<QuickBooksImportPreviewRow>>(cut.Instance, "PreviewRows"));
		Assert.Null(GetPrivateField<QuickBooksImportPreviewResponse?>(cut.Instance, "PreviewResponse"));
	}

	[Fact]
	public async Task CommitImportAsync_CompletesImport_AndClearsSelectedFile()
	{
		var state = new WorkspaceState();
		var service = CreateImportService(request =>
		{
			if (request.RequestUri?.AbsolutePath.EndsWith("/commit", StringComparison.Ordinal) == true)
			{
				return CreateJsonResponse(new QuickBooksImportCommitResponse(
					"quickbooks-ledger.csv",
					"file-hash",
					WorkspaceTestData.WaterUtility,
					WorkspaceTestData.WaterFiscalYear,
					2,
					123,
					false,
					"Imported 2 QuickBooks rows.",
					[]));
			}

			return CreateJsonResponse(CreatePreviewResponse(isDuplicate: false));
		});

		Services.AddSingleton(state);
		Services.AddSingleton(service);
		Services.AddSyncfusionBlazor();

		SetRendererInfo(new RendererInfo("Server", true));
		JSInterop.Mode = JSRuntimeMode.Loose;

		var cut = RenderComponent<QuickBooksImportPanel>(parameters => parameters
			.Add(panel => panel.WorkspaceState, state));

		SeedPreviewState(cut, includeAssistantQuestion: false);

		await InvokePrivateAsync(cut, "CommitImportAsync");

		var committedResponse = GetPrivateField<QuickBooksImportPreviewResponse?>(cut.Instance, "PreviewResponse");

		Assert.Equal("Import complete", GetPrivateField<string>(cut.Instance, "StatusHeadline"));
		Assert.Equal("Imported 2 QuickBooks rows.", GetPrivateField<string>(cut.Instance, "StatusMessage"));
		Assert.Equal(3, GetPrivateField<int>(cut.Instance, "ActiveStep"));
		Assert.Equal(100, GetPrivateField<int>(cut.Instance, "ImportProgress"));
		Assert.Null(GetPrivateField<byte[]?>(cut.Instance, "SelectedFileBytes"));
		Assert.NotNull(committedResponse);
		Assert.False(committedResponse!.IsDuplicate);
		Assert.Contains("file duplicate = False", GetPrivateField<string>(cut.Instance, "AssistantContextSummary"));
	}

	[Fact]
	public async Task AskAssistantAsync_ReturnsGuidanceForLoadedPreview()
	{
		var state = new WorkspaceState();
		var service = CreateImportService(request =>
		{
			if (request.RequestUri?.AbsolutePath.EndsWith("/assistant", StringComparison.Ordinal) == true)
			{
				return CreateJsonResponse(new QuickBooksImportGuidanceResponse(
					"Why would this file be blocked as a duplicate?",
					"The file hash matches a prior QuickBooks import.",
					false,
					$"quickbooks-ledger.csv for {WorkspaceTestData.WaterUtility} FY {WorkspaceTestData.WaterFiscalYear}: 2 rows parsed, 0 duplicates flagged, file duplicate = False."));
			}

			return CreateJsonResponse(CreatePreviewResponse(isDuplicate: false));
		});

		Services.AddSingleton(state);
		Services.AddSingleton(service);
		Services.AddSyncfusionBlazor();

		SetRendererInfo(new RendererInfo("Server", true));
		JSInterop.Mode = JSRuntimeMode.Loose;

		var cut = RenderComponent<QuickBooksImportPanel>(parameters => parameters
			.Add(panel => panel.WorkspaceState, state));

		SeedPreviewState(cut, includeAssistantQuestion: false);
		SetPrivateField(cut.Instance, "AssistantQuestion", "Why would this file be blocked as a duplicate?");

		await InvokePrivateAsync(cut, "AskAssistantAsync");

		Assert.Equal("The file hash matches a prior QuickBooks import.", GetPrivateField<string>(cut.Instance, "AssistantAnswer"));
		Assert.Equal(WorkspaceTestData.QuickBooksAssistantContextSummary, GetPrivateField<string>(cut.Instance, "AssistantContextSummary"));
		Assert.Equal(2, GetPrivateField<int>(cut.Instance, "ActiveStep"));
	}

	[Fact]
	public async Task AskAssistantAsync_ReportsApiFailureDetails_FromPlainText()
	{
		var state = new WorkspaceState();
		var service = CreateImportService(request =>
		{
			if (request.RequestUri?.AbsolutePath.EndsWith("/assistant", StringComparison.Ordinal) == true)
			{
				return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
				{
					Content = new StringContent("assistant service unavailable", Encoding.UTF8, "text/plain")
				};
			}

			return CreateJsonResponse(CreatePreviewResponse(isDuplicate: false));
		});

		Services.AddSingleton(state);
		Services.AddSingleton(service);
		Services.AddSyncfusionBlazor();

		SetRendererInfo(new RendererInfo("Server", true));
		JSInterop.Mode = JSRuntimeMode.Loose;

		var cut = RenderComponent<QuickBooksImportPanel>(parameters => parameters
			.Add(panel => panel.WorkspaceState, state));

		SeedPreviewState(cut, includeAssistantQuestion: false);
		SetPrivateField(cut.Instance, "AssistantQuestion", "Why would this file be blocked as a duplicate?");

		await InvokePrivateAsync(cut, "AskAssistantAsync");

		Assert.Equal("QuickBooks import assistance failed with status 503: assistant service unavailable", GetPrivateField<string>(cut.Instance, "AssistantAnswer"));
	}

	[Fact]
	public async Task ClearSelectionAsync_ResetsThePanelToItsInitialState()
	{
		var state = new WorkspaceState();
		var service = CreateImportService(_ => CreateJsonResponse(CreatePreviewResponse(isDuplicate: false)));

		Services.AddSingleton(state);
		Services.AddSingleton(service);
		Services.AddSyncfusionBlazor();

		SetRendererInfo(new RendererInfo("Server", true));
		JSInterop.Mode = JSRuntimeMode.Loose;

		var cut = RenderComponent<QuickBooksImportPanel>(parameters => parameters
			.Add(panel => panel.WorkspaceState, state));

		SeedPreviewState(cut, includeAssistantQuestion: true);
		SetPrivateField(cut.Instance, "ShowCommitDialog", true);
		SetPrivateField(cut.Instance, "SelectedFileName", "quickbooks-ledger.csv");

		await InvokePrivateAsync(cut, "ClearSelectionAsync");

		Assert.Equal(string.Empty, GetPrivateField<string>(cut.Instance, "SelectedFileName"));
		Assert.Null(GetPrivateField<byte[]?>(cut.Instance, "SelectedFileBytes"));
		Assert.Empty(GetPrivateField<List<QuickBooksImportPreviewRow>>(cut.Instance, "PreviewRows"));
		Assert.Null(GetPrivateField<QuickBooksImportPreviewResponse?>(cut.Instance, "PreviewResponse"));
		Assert.Equal(string.Empty, GetPrivateField<string>(cut.Instance, "AssistantQuestion"));
		Assert.Equal("Ready", GetPrivateField<string>(cut.Instance, "StatusHeadline"));
		Assert.Equal("Choose a QuickBooks export to begin.", GetPrivateField<string>(cut.Instance, "StatusMessage"));
		Assert.Equal(0, GetPrivateField<int>(cut.Instance, "ActiveStep"));
		Assert.Equal(0, GetPrivateField<int>(cut.Instance, "ImportProgress"));
		Assert.False(GetPrivateField<bool>(cut.Instance, "ShowCommitDialog"));
	}

	[Fact]
	public void OnInitializedAsync_LoadsRoutingWorkspace_AndRendersBoundRoutingData()
	{
		var state = new WorkspaceState();
		var service = new QuickBooksImportApiService(new HttpClient(new StubHttpMessageHandler(request =>
		{
			var path = request.RequestUri?.AbsolutePath ?? string.Empty;

			if (path.EndsWith("/routing", StringComparison.Ordinal))
			{
				return CreateJsonResponse(new QuickBooksRoutingConfigurationResponse
				{
					StatusMessage = "Loaded 1 routing rule.",
					Rules =
					[
						new QuickBooksRoutingRuleDefinition
						{
							Id = 5,
							Name = "Brookside to Apartments",
							Description = "Route Brookside costs to Apartments.",
							Priority = 10,
							IsActive = true,
							MemoPattern = "BROOKSIDE",
							TargetEnterprise = "Apartments",
							AllocationProfileId = 3
						}
					],
					AllocationProfiles =
					[
						new QuickBooksAllocationProfileDefinition
						{
							Id = 3,
							Name = "Administrative split",
							Description = "Split administrative overhead.",
							IsActive = true,
							Targets =
							[
								new QuickBooksAllocationTargetDefinition
								{
									Id = 30,
									EnterpriseName = "Apartments",
									AllocationPercent = 40m
								},
								new QuickBooksAllocationTargetDefinition
								{
									Id = 31,
									EnterpriseName = "Trash",
									AllocationPercent = 60m
								}
							]
						}
					]
				});
			}

			if (path.EndsWith("/history", StringComparison.Ordinal))
			{
				return CreateJsonResponse(new QuickBooksImportHistoryResponse
				{
					StatusMessage = "Loaded 1 import history item.",
					Items =
					[
						new QuickBooksImportHistoryItem
						{
							SourceFileId = 77,
							BatchId = 91,
							FileName = "brookside-journal.csv",
							ScopeSummary = "Apartments, Trash",
							RowCount = 2,
							ImportedAtUtc = "2026-04-22T00:00:00Z"
						}
					]
				});
			}

			return CreateJsonResponse(CreatePreviewResponse(isDuplicate: false));
		}))
		{
			BaseAddress = new Uri("https://workspace.local/")
		});

		Services.AddSingleton(state);
		Services.AddSingleton(service);
		Services.AddSyncfusionBlazor();

		SetRendererInfo(new RendererInfo("Server", true));
		JSInterop.Mode = JSRuntimeMode.Loose;

		var cut = RenderComponent<QuickBooksImportPanel>(parameters => parameters
			.Add(panel => panel.WorkspaceState, state));

		cut.WaitForAssertion(() =>
		{
			Assert.Equal("Loaded 1 routing rule.", GetPrivateField<string>(cut.Instance, "RoutingStatusMessage"));
			Assert.Equal("Loaded 1 import history item.", GetPrivateField<string>(cut.Instance, "ImportHistoryStatusMessage"));
			var loadedRules = GetPrivateField<List<QuickBooksRoutingRuleDefinition>>(cut.Instance, "RoutingRules");
			var loadedProfiles = GetPrivateField<List<QuickBooksAllocationProfileDefinition>>(cut.Instance, "AllocationProfiles");
			var loadedHistory = GetPrivateField<List<QuickBooksImportHistoryItem>>(cut.Instance, "ImportHistoryItems");

			var loadedRule = Assert.Single(loadedRules);
			var loadedProfile = Assert.Single(loadedProfiles);
			var loadedHistoryItem = Assert.Single(loadedHistory);

			Assert.Equal("Brookside to Apartments", loadedRule.Name);
			Assert.Equal("BROOKSIDE", loadedRule.MemoPattern);
			Assert.Equal("Administrative split", loadedProfile.Name);
			Assert.Equal("brookside-journal.csv", loadedHistoryItem.FileName);
			Assert.Equal("Apartments, Trash", loadedHistoryItem.ScopeSummary);
			Assert.Contains("Brookside to Apartments", cut.Markup);
		});
	}

	[Fact]
	public async Task SaveRoutingConfigurationAsync_PersistsEditedRoutingModels()
	{
		var state = new WorkspaceState();
		string? savedRoutingJson = null;
		var service = new QuickBooksImportApiService(new HttpClient(new StubHttpMessageHandler(request =>
		{
			var path = request.RequestUri?.AbsolutePath ?? string.Empty;

			if (path.EndsWith("/routing", StringComparison.Ordinal) && request.Method == HttpMethod.Put)
			{
				savedRoutingJson = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
				return CreateJsonResponse(new QuickBooksRoutingConfigurationResponse
				{
					StatusMessage = "Saved QuickBooks routing configuration.",
					Rules =
					[
						new QuickBooksRoutingRuleDefinition
						{
							Id = 9,
							Name = "Reserve transfer to Apartments",
							Priority = 20,
							IsActive = true,
							MemoPattern = "APARTMENT RESERVE",
							TargetEnterprise = "Apartments",
							AllocationProfileId = 12
						}
					],
					AllocationProfiles =
					[
						new QuickBooksAllocationProfileDefinition
						{
							Id = 12,
							Name = "Reserve split",
							Description = "Reserve allocation.",
							IsActive = true,
							Targets =
							[
								new QuickBooksAllocationTargetDefinition
								{
									EnterpriseName = "Apartments",
									AllocationPercent = 100m
								}
							]
						}
					]
				});
			}

			return CreateJsonResponse(new QuickBooksRoutingConfigurationResponse
			{
				StatusMessage = "Loaded QuickBooks routing configuration.",
				Rules = [],
				AllocationProfiles = []
			});
		}))
		{
			BaseAddress = new Uri("https://workspace.local/")
		});

		Services.AddSingleton(state);
		Services.AddSingleton(service);
		Services.AddSyncfusionBlazor();

		SetRendererInfo(new RendererInfo("Server", true));
		JSInterop.Mode = JSRuntimeMode.Loose;

		var cut = RenderComponent<QuickBooksImportPanel>(parameters => parameters
			.Add(panel => panel.WorkspaceState, state));

		var routingRules = GetPrivateField<List<QuickBooksRoutingRuleDefinition>>(cut.Instance, "RoutingRules");
		var allocationProfiles = GetPrivateField<List<QuickBooksAllocationProfileDefinition>>(cut.Instance, "AllocationProfiles");

		routingRules.Add(new QuickBooksRoutingRuleDefinition
		{
			Name = "Reserve transfer to Apartments",
			Priority = 20,
			IsActive = true,
			MemoPattern = "APARTMENT RESERVE",
			TargetEnterprise = "Apartments",
			AllocationProfileId = 12
		});

		allocationProfiles.Add(new QuickBooksAllocationProfileDefinition
		{
			Id = 12,
			Name = "Reserve split",
			Description = "Reserve allocation.",
			IsActive = true,
			Targets =
			[
				new QuickBooksAllocationTargetDefinition
				{
					EnterpriseName = "Apartments",
					AllocationPercent = 100m
				}
			]
		});

		await InvokePrivateAsync(cut, "SaveRoutingConfigurationAsync");

		Assert.False(string.IsNullOrWhiteSpace(savedRoutingJson));
		var savedRequest = JsonSerializer.Deserialize<QuickBooksRoutingConfigurationRequest>(savedRoutingJson!, JsonOptions);
		Assert.NotNull(savedRequest);
		var savedRule = Assert.Single(savedRequest!.Rules);
		Assert.Equal("Reserve transfer to Apartments", savedRule.Name);
		Assert.Equal("APARTMENT RESERVE", savedRule.MemoPattern);
		Assert.Equal("Apartments", savedRule.TargetEnterprise);
		Assert.Equal(12, savedRule.AllocationProfileId);

		var savedProfile = Assert.Single(savedRequest.AllocationProfiles);
		Assert.Equal("Reserve split", savedProfile.Name);
		Assert.Equal(100m, Assert.Single(savedProfile.Targets).AllocationPercent);
		Assert.Equal("Saved QuickBooks routing configuration.", GetPrivateField<string>(cut.Instance, "RoutingStatusMessage"));
	}

	private static QuickBooksImportApiService CreateImportService(Func<HttpRequestMessage, HttpResponseMessage> responder)
	{
		return new QuickBooksImportApiService(new HttpClient(new StubHttpMessageHandler(request =>
		{
			var path = request.RequestUri?.AbsolutePath ?? string.Empty;

			if (path.EndsWith("/routing", StringComparison.Ordinal))
			{
				return CreateJsonResponse(new QuickBooksRoutingConfigurationResponse
				{
					StatusMessage = "Loaded QuickBooks routing configuration.",
					Rules = [],
					AllocationProfiles = []
				});
			}

			if (path.EndsWith("/history", StringComparison.Ordinal))
			{
				return CreateJsonResponse(new QuickBooksImportHistoryResponse
				{
					StatusMessage = "No QuickBooks import history is available yet.",
					Items = []
				});
			}

			if (path.EndsWith("/reroute", StringComparison.Ordinal))
			{
				return CreateJsonResponse(new QuickBooksHistoricalRerouteResponse
				{
					SourceFileId = 1,
					FileName = "quickbooks-ledger.csv",
					SourceRowCount = 2,
					RoutedRowCount = 2,
					StatusMessage = "Reapplied QuickBooks routing."
				});
			}

			return responder(request);
		}))
		{
			BaseAddress = new Uri("https://workspace.local/")
		});
	}

	private static QuickBooksImportPreviewResponse CreatePreviewResponse(bool isDuplicate)
	{
		var rows = new List<QuickBooksImportPreviewRow>
		{
			new(1, "01/01/2026", "Invoice", "1001", "Town of Wiley", "Water Billing", "Water Revenue", "Accounts Receivable", 125m, 125m, "C", isDuplicate),
			new(2, "01/02/2026", "Payment", "1002", "Town of Wiley", "Payment Received", "Accounts Receivable", "Water Revenue", -125m, 0m, "C", isDuplicate)
		};

		return new QuickBooksImportPreviewResponse(
			"quickbooks-ledger.csv",
			"file-hash",
			WorkspaceTestData.WaterUtility,
			WorkspaceTestData.WaterFiscalYear,
			rows.Count,
			isDuplicate ? 1 : 0,
			isDuplicate,
			isDuplicate ? "Duplicate QuickBooks file blocked." : "Preview loaded for quickbooks-ledger.csv.",
			rows);
	}

	private static void SeedPreviewState(IRenderedComponent<QuickBooksImportPanel> cut, bool includeAssistantQuestion)
	{
		SetPrivateField(cut.Instance, "SelectedFileBytes", new byte[] { 4, 3, 2, 1 });
		SetPrivateField(cut.Instance, "SelectedFileName", "quickbooks-ledger.csv");
		SetPrivateField(cut.Instance, "PreviewResponse", CreatePreviewResponse(isDuplicate: false));
		SetPrivateField(cut.Instance, "AssistantQuestion", includeAssistantQuestion ? "Why would this file be blocked as a duplicate?" : string.Empty);
		SetPrivateField(cut.Instance, "AssistantAnswer", "Load a QuickBooks preview, then ask a question about the rows or troubleshooting steps.");
		SetPrivateField(cut.Instance, "AssistantContextSummary", $"quickbooks-ledger.csv for {WorkspaceTestData.WaterUtility} FY {WorkspaceTestData.WaterFiscalYear}: 2 rows parsed, 0 duplicates flagged, file duplicate = False.");
		SetPrivateField(cut.Instance, "StatusHeadline", "Preview ready");
		SetPrivateField(cut.Instance, "StatusMessage", "Preview loaded for quickbooks-ledger.csv.");
		SetPrivateField(cut.Instance, "ActiveStep", 1);
		SetPrivateField(cut.Instance, "ImportProgress", 75);

		var previewRows = GetPrivateField<List<QuickBooksImportPreviewRow>>(cut.Instance, "PreviewRows");
		previewRows.Clear();
		previewRows.AddRange(CreatePreviewResponse(isDuplicate: false).Rows);
	}

	private static async Task InvokePrivateAsync(IRenderedComponent<QuickBooksImportPanel> cut, string methodName, params object?[]? args)
	{
		await cut.InvokeAsync(async () =>
		{
			var method = cut.Instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
			Assert.NotNull(method);

			if (method!.Invoke(cut.Instance, args) is Task task)
			{
				await task;
			}
		});
	}

	private static T GetPrivateField<T>(object instance, string fieldName)
	{
		var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
		if (field is not null)
		{
			return (T)field.GetValue(instance)!;
		}

		var property = instance.GetType().GetProperty(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
		Assert.NotNull(property);
		return (T)property!.GetValue(instance)!;
	}

	private static void SetPrivateField<T>(object instance, string fieldName, T value)
	{
		var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
		if (field is not null)
		{
			field.SetValue(instance, value);
			return;
		}

		var property = instance.GetType().GetProperty(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
		Assert.NotNull(property);
		property!.SetValue(instance, value);
	}

	private static HttpResponseMessage CreateJsonResponse<TValue>(TValue value)
	{
		return new HttpResponseMessage(HttpStatusCode.OK)
		{
			Content = new StringContent(JsonSerializer.Serialize(value, JsonOptions), Encoding.UTF8, "application/json")
		};
	}

	#pragma warning disable S1144
	private sealed class StubHttpMessageHandler : HttpMessageHandler
	{
		private readonly Func<HttpRequestMessage, HttpResponseMessage> responder;

		public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
		{
			this.responder = responder;
		}

		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			_ = cancellationToken;
			return Task.FromResult(responder(request));
		}
	}
	#pragma warning restore S1144
}