using System.Net;
using System.Text;
using System.Text.Json;
using Bunit;
using Bunit.JSInterop;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Syncfusion.Blazor;
using WileyCoWeb.Components.Pages;
using WileyCoWeb.Contracts;
using WileyCoWeb.Services;
using WileyCoWeb.State;

namespace WileyCoWeb.ComponentTests;

public sealed class WileyWorkspaceBaseTests : TestContext
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
	{
		PropertyNameCaseInsensitive = true
	};

	[Fact]
	public async Task SaveWorkspaceBaselineAsync_PersistsSnapshotAndUpdatesWorkspaceState()
	{
		var state = new WorkspaceState();
		var handler = new StubHttpMessageHandler(request =>
		{
			if (IsRequest(request, HttpMethod.Get, "/api/workspace/scenarios"))
			{
				return CreateJsonResponse(new WorkspaceScenarioCollectionResponse([]));
			}

			if (IsRequest(request, HttpMethod.Put, "/api/workspace/baseline"))
			{
				return CreateJsonResponse(new WorkspaceBaselineUpdateResponse(
					"Water",
					2026,
					"2026-04-07T12:00:00Z",
					"Baseline saved successfully.",
					new WorkspaceBootstrapData(
						"Water Utility",
						2026,
						"Base Planning Scenario",
						31.75m,
						450000m,
						15000m,
						"2026-04-07T12:00:00Z")));
			}

			throw new InvalidOperationException($"Unexpected request {request.Method} {request.RequestUri}");
		});

		var cut = RenderWorkspace(state, handler);
		cut.WaitForAssertion(() => Assert.Contains("No saved scenarios found for Water FY 2026.", cut.Markup));

		await ClickButtonAsync(cut, "Save workspace baseline");

		cut.WaitForAssertion(() => Assert.Contains("Baseline saved successfully.", cut.Markup));

		Assert.Equal("Water Utility", state.SelectedEnterprise);
		Assert.Equal(2026, state.SelectedFiscalYear);
		Assert.Equal(31.75m, state.CurrentRate);
		Assert.Equal(450000m, state.TotalCosts);
		Assert.Equal(15000m, state.ProjectedVolume);
		Assert.Contains("after baseline save.", cut.Markup);
	}

	[Fact]
	public async Task SaveWorkspaceBaselineAsync_WhenApiReturnsError_ReportsFailureStatus()
	{
		var state = new WorkspaceState();
		var handler = new StubHttpMessageHandler(request =>
		{
			if (IsRequest(request, HttpMethod.Get, "/api/workspace/scenarios"))
			{
				return CreateJsonResponse(new WorkspaceScenarioCollectionResponse([]));
			}

			if (IsRequest(request, HttpMethod.Put, "/api/workspace/baseline"))
			{
				return new HttpResponseMessage(HttpStatusCode.InternalServerError)
				{
					Content = new StringContent("baseline rejected", Encoding.UTF8, "text/plain")
				};
			}

			throw new InvalidOperationException($"Unexpected request {request.Method} {request.RequestUri}");
		});

		var cut = RenderWorkspace(state, handler);
		cut.WaitForAssertion(() => Assert.Contains("No saved scenarios found for Water FY 2026.", cut.Markup));

		await ClickButtonAsync(cut, "Save workspace baseline");

		cut.WaitForAssertion(() => Assert.Contains("Baseline save failed: Saving the workspace baseline failed with status 500: baseline rejected", cut.Markup));
		Assert.Equal(28.50m, state.CurrentRate);
		Assert.Equal(412500m, state.TotalCosts);
		Assert.Equal(14500m, state.ProjectedVolume);
	}

	[Fact]
	public async Task SaveScenarioAsync_AndApplySelectedScenario_RefreshWorkspaceState()
	{
		var state = new WorkspaceState();
		var scenarioSaved = false;
		var savedScenario = new WorkspaceScenarioSummaryResponse(
			42,
			"Council Approved Scenario",
			"Water",
			2026,
			"2026-04-07T12:00:00Z",
			28.50m,
			412500m,
			14500m,
			0m,
			0,
			"Council approved");
		var appliedSnapshot = new WorkspaceBootstrapData(
			"Water",
			2026,
			"Council Approved Scenario",
			31.00m,
			430000m,
			14000m,
			"2026-04-07T12:05:00Z");
		var handler = new StubHttpMessageHandler(request =>
		{
			if (IsRequest(request, HttpMethod.Get, "/api/workspace/scenarios"))
			{
				return CreateJsonResponse(new WorkspaceScenarioCollectionResponse(scenarioSaved ? [savedScenario] : []));
			}

			if (IsRequest(request, HttpMethod.Post, "/api/workspace/scenarios"))
			{
				scenarioSaved = true;
				return CreateJsonResponse(savedScenario);
			}

			if (IsRequest(request, HttpMethod.Get, "/api/workspace/scenarios/42"))
			{
				return CreateJsonResponse(appliedSnapshot);
			}

			throw new InvalidOperationException($"Unexpected request {request.Method} {request.RequestUri}");
		});

		var cut = RenderWorkspace(state, handler);
		cut.WaitForAssertion(() => Assert.Contains("No saved scenarios found for Water FY 2026.", cut.Markup));

		await ClickButtonAsync(cut, "Save scenario");
		cut.WaitForAssertion(() => Assert.Contains("Saved scenario 'Council Approved Scenario' at 2026-04-07T12:00:00Z.", cut.Markup));
		Assert.Equal("Council Approved Scenario", state.ActiveScenarioName);

		await ClickButtonAsync(cut, "Apply saved scenario");
		cut.WaitForAssertion(() => Assert.Contains("Applied saved scenario 'Council Approved Scenario'.", cut.Markup));

		Assert.Equal("Council Approved Scenario", state.ActiveScenarioName);
		Assert.Equal(31.00m, state.CurrentRate);
		Assert.Equal(430000m, state.TotalCosts);
		Assert.Equal(14000m, state.ProjectedVolume);
		Assert.Contains("Loaded Water FY 2026 | Council Approved Scenario from saved scenario.", cut.Markup);
	}

	[Fact]
	public void WorkspaceShell_RendersDocumentExports_AndTrendPanel()
	{
		var state = new WorkspaceState();
		var handler = new StubHttpMessageHandler(request =>
		{
			if (IsRequest(request, HttpMethod.Get, "/api/workspace/scenarios"))
			{
				return CreateJsonResponse(new WorkspaceScenarioCollectionResponse([]));
			}

			throw new InvalidOperationException($"Unexpected request {request.Method} {request.RequestUri}");
		});

		var cut = RenderWorkspace(state, handler);

		Assert.Contains("Export customers to Excel", cut.Markup);
		Assert.Contains("Export scenario to Excel", cut.Markup);
		Assert.Contains("Download PDF rate packet", cut.Markup);
		Assert.Contains("Trends & Projections", cut.Markup);
		Assert.Contains("Historical and Projected Rates", cut.Markup);
	}

	private IRenderedComponent<WileyWorkspace> RenderWorkspace(WorkspaceState state, StubHttpMessageHandler handler)
	{
		Services.AddSingleton(state);
		Services.AddSingleton(new QuickBooksImportApiService(new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
		{
			Content = new StringContent("{}", Encoding.UTF8, "application/json")
		}))
		{
			BaseAddress = new Uri("https://workspace.local/")
		}));
		Services.AddSingleton(new WorkspacePersistenceService(new NoopJsRuntime(), state));
		Services.AddSingleton(new WorkspaceSnapshotApiService(new HttpClient(handler)
		{
			BaseAddress = new Uri("https://workspace.local/")
		}));
		Services.AddSingleton(new WorkspaceAiApiService(new HttpClient(new StubHttpMessageHandler(_ => CreateJsonResponse(new WorkspaceChatResponse(string.Empty, string.Empty, false, string.Empty))))
		{
			BaseAddress = new Uri("https://workspace.local/")
		}));
		Services.AddSingleton(new WorkspaceDocumentExportService());
		Services.AddSingleton(new BrowserDownloadService(new NoopJsRuntime()));
		Services.AddSyncfusionBlazor();

		SetRendererInfo(new RendererInfo("Server", true));
		JSInterop.Mode = JSRuntimeMode.Loose;

		return RenderComponent<WileyWorkspace>();
	}

	private static async Task ClickButtonAsync(IRenderedComponent<WileyWorkspace> cut, string buttonText)
	{
		var button = cut.FindAll("button").First(candidate => candidate.TextContent.Contains(buttonText, StringComparison.Ordinal));
		await button.ClickAsync(new MouseEventArgs());
	}

	private static bool IsRequest(HttpRequestMessage request, HttpMethod method, string path)
	{
		return request.Method == method && string.Equals(request.RequestUri?.AbsolutePath, path, StringComparison.Ordinal);
	}

	private static HttpResponseMessage CreateJsonResponse<TValue>(TValue value)
	{
		return new HttpResponseMessage(HttpStatusCode.OK)
		{
			Content = new StringContent(JsonSerializer.Serialize(value, JsonOptions), Encoding.UTF8, "application/json")
		};
	}

	private sealed class NoopJsRuntime : IJSRuntime
	{
		private int invocationCount;

		ValueTask<TValue> IJSRuntime.InvokeAsync<TValue>(string identifier, object?[]? args)
		{
			_ = identifier;
			_ = args;
			invocationCount++;
			if (invocationCount < 0)
			{
				throw new InvalidOperationException("Unexpected JS invocation count.");
			}

			return ValueTask.FromResult(default(TValue)!);
		}

		ValueTask<TValue> IJSRuntime.InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
		{
			_ = identifier;
			_ = cancellationToken;
			_ = args;
			invocationCount++;
			if (invocationCount < 0)
			{
				throw new InvalidOperationException("Unexpected JS invocation count.");
			}

			return ValueTask.FromResult(default(TValue)!);
		}
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