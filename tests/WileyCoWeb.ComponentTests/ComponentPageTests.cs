using System.Net;
using System.Text;
using System.Text.Json;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Syncfusion.Blazor;
using WileyCoWeb.Components;
using WileyCoWeb.Components.Layout;
using WileyCoWeb.Components.Pages;
using WileyCoWeb.Services;
using WileyCoWeb.State;

namespace WileyCoWeb.ComponentTests;

public sealed class ComponentPageTests
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	[Fact]
	public void App_RendersWorkspaceLandingPage_FromRootRoute()
	{
		using var context = CreateContext();

		var cut = context.RenderComponent<App>();

		Assert.Contains("Utility Rate Study Workspace", cut.Markup);
		Assert.Contains("JARVIS AI", cut.Markup);
	}

	[Fact]
	public void MainLayout_RendersNavigationChrome_AndBodyContent()
	{
		using var context = CreateContext();

		var cut = context.RenderComponent<MainLayout>(parameters => parameters
			.Add(p => p.Body, (RenderFragment)(builder => builder.AddMarkupContent(0, "<h1>Workspace Body</h1>"))));

		Assert.Contains("Workspace Body", cut.Markup);
		Assert.Contains("About", cut.Markup);
		Assert.Contains("Reload", cut.Markup);
	}

	[Fact]
	public void NavMenu_RendersExpectedWorkspaceLinks()
	{
		using var context = CreateContext();

		var cut = context.RenderComponent<NavMenu>();

		Assert.Contains("WileyCoWeb", cut.Markup);
		Assert.Contains("Workspace", cut.Markup);
		Assert.Contains("Budget Dashboard", cut.Markup);
		Assert.Contains("Rebuild Plan", cut.Markup);
	}

	[Fact]
	public void ErrorPage_RendersSupportMessage()
	{
		using var context = CreateContext();

		var cut = context.RenderComponent<Error>();

		Assert.Contains("An unexpected error occurred while loading the app.", cut.Markup);
		Assert.Contains("Refresh the page or return to the home page and try again.", cut.Markup);
	}

	[Fact]
	public void CounterPage_IncrementsCurrentCount_WhenButtonIsClicked()
	{
		using var context = CreateContext();

		var cut = context.RenderComponent<Counter>();

		Assert.Contains("Current count: 0", cut.Markup);

		cut.Find("button").Click();

		Assert.Contains("Current count: 1", cut.Markup);
	}

	[Fact]
	public void WeatherPage_ShowsLoading_ThenForecastRows()
	{
		using var context = CreateContext();

		var cut = context.RenderComponent<Weather>();

		Assert.Contains("Loading...", cut.Markup);

		cut.WaitForAssertion(() =>
		{
			Assert.True(cut.FindAll("tbody tr").Count > 0);
		});
	}

	[Fact]
	public void BudgetDashboard_RendersCorePlanningSections()
	{
		using var context = CreateContext();

		var cut = context.RenderComponent<BudgetDashboard>();

		Assert.Contains("Budget Dashboard", cut.Markup);
		Assert.Contains("KPI Summary", cut.Markup);
		Assert.Contains("Editable Budget Table", cut.Markup);
		Assert.Contains("Budget vs Actual", cut.Markup);
		Assert.Contains("Detailed Line Items", cut.Markup);
	}

	[Fact]
	public void WileyWorkspace_RendersSummaryMetrics_AndChatPanel()
	{
		using var context = CreateContext();

		var cut = context.RenderComponent<WileyWorkspace>();

		Assert.Contains("Utility Rate Study Workspace", cut.Markup);
		Assert.Contains("Enterprise", cut.Markup);
		Assert.Contains("Break-Even", cut.Markup);
		Assert.Contains("JARVIS AI", cut.Markup);
	}

	[Fact]
	public void WileyWorkspace_SavesAndLoadsScenarioSnapshot()
	{
		using var context = CreateContext();
		var workspaceState = context.Services.GetRequiredService<WorkspaceState>();

		workspaceState.SetSelection("Trash", 2025);
		workspaceState.SetActiveScenarioName("Draft scenario");
		workspaceState.SetCurrentRate(44.75m);
		workspaceState.SetTotalCosts(220000m);
		workspaceState.SetProjectedVolume(6400m);
		workspaceState.AddScenarioItem("Truck replacement", 2500m);

		var cut = context.RenderComponent<WileyWorkspace>();

		cut.FindAll("button").Single(button => button.TextContent.Contains("Save scenario", StringComparison.OrdinalIgnoreCase)).Click();

		cut.WaitForAssertion(() =>
		{
			Assert.Contains("Saved workspace snapshot", cut.Markup, StringComparison.OrdinalIgnoreCase);
		});

		workspaceState.SetSelection("Sewer", 2027);
		workspaceState.SetActiveScenarioName("Changed scenario");
		workspaceState.SetCurrentRate(12.25m);

		cut.FindAll("button").Single(button => button.TextContent.Contains("Load scenario", StringComparison.OrdinalIgnoreCase)).Click();

		cut.WaitForAssertion(() =>
		{
			Assert.Equal("Water Utility", workspaceState.SelectedEnterprise);
			Assert.Equal(2026, workspaceState.SelectedFiscalYear);
			Assert.Equal("Water Utility planning snapshot", workspaceState.ActiveScenarioName);
		});
	}

	[Fact]
	public async Task JarvisChatPanel_SendsConversation_AndClearsItAgain()
	{
		using var context = CreateContext();

		var cut = context.RenderComponent<JarvisChatPanel>();

		Assert.Contains("JARVIS", cut.Markup);
		Assert.Contains("Suggested prompt", cut.Markup);

		var input = cut.Find("input");
		input.Change("What if trash costs rise 5%?");

		cut.FindAll("button").Single(button => button.TextContent.Contains("Ask JARVIS", StringComparison.OrdinalIgnoreCase)).Click();

		Assert.Contains("JARVIS is analyzing the rates", cut.Markup);

		await Task.Delay(350);

		Assert.Contains("You", cut.Markup);
		Assert.Contains("trash", cut.Markup, StringComparison.OrdinalIgnoreCase);

		cut.FindAll("button").Single(button => button.TextContent.Contains("Clear", StringComparison.OrdinalIgnoreCase)).Click();

		Assert.Contains("Conversation cleared", cut.Markup);
		Assert.DoesNotContain("What if trash costs rise 5%?", cut.Markup);
	}

	private static TestContext CreateContext()
	{
		var context = new TestContext();

		var workspaceState = new WorkspaceState();
		var jsRuntime = new FakeJsRuntime();

		context.Services.AddSingleton(workspaceState);
		context.Services.AddSingleton<IJSRuntime>(jsRuntime);
		context.Services.AddScoped(_ => new WorkspacePersistenceService(jsRuntime, workspaceState));
		var snapshotClient = CreateSnapshotClient();
		context.Services.AddScoped(_ => new WorkspaceSnapshotApiService(snapshotClient));
		context.Services.AddScoped(_ => new WorkspaceBootstrapService(snapshotClient, workspaceState));
		context.Services.AddSyncfusionBlazor();
		context.Renderer.SetRendererInfo(new RendererInfo("WebAssembly", true));

		return context;
	}

	private static HttpClient CreateSnapshotClient()
	{
		return new HttpClient(new RoutedHttpMessageHandler(request =>
		{
			if (request.Method == HttpMethod.Get && request.RequestUri?.AbsolutePath.EndsWith("/api/workspace/snapshot", StringComparison.OrdinalIgnoreCase) == true)
			{
				return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
				{
					Content = new StringContent(JsonSerializer.Serialize(new WorkspaceBootstrapData(
						"Water Utility",
						2026,
						"Water Utility planning snapshot",
						31.25m,
						98000m,
						4500m,
						"2026-04-05T12:00:00Z")
					{
						ScenarioItems = [new WorkspaceScenarioItemData(Guid.NewGuid(), "Operations reserve", 1500m)],
						CustomerRows = [new CustomerRow("Dana", "Water", "Yes")],
						ProjectionRows = [new ProjectionRow("FY25", 29.10m), new ProjectionRow("FY26", 31.25m)]
					}, JsonOptions), Encoding.UTF8, "application/json")
				});
			}

			if (request.Method != HttpMethod.Post || request.RequestUri?.AbsolutePath.EndsWith("/api/workspace/snapshot", StringComparison.OrdinalIgnoreCase) != true)
			{
				return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
			}

			return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
			{
				Content = new StringContent(JsonSerializer.Serialize(new WorkspaceSnapshotSaveResponse(42, "workspace snapshot", "2026-04-05T12:00:00Z")), Encoding.UTF8, "application/json")
			});
		}))
		{
			BaseAddress = new Uri("https://example.test/")
		};
	}

	#pragma warning disable
	private sealed class FakeJsRuntime : IJSRuntime
	{
		private readonly Dictionary<string, string?> storage = new(StringComparer.Ordinal);
		private readonly List<string> calls = new();

		public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
		{
			return InvokeAsync<TValue>(identifier, CancellationToken.None, args);
		}

		public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
		{
			cancellationToken.ThrowIfCancellationRequested();
			var arguments = args ?? Array.Empty<object?>();
			calls.Add(identifier);

			object? result = identifier switch
			{
				"wileyWorkspaceStorage.getItem" => storage.TryGetValue(arguments[0]?.ToString() ?? string.Empty, out var storedValue) ? storedValue : null,
				"wileyWorkspaceStorage.setItem" => StoreValue(arguments),
				"wileyWorkspaceStorage.removeItem" => RemoveValue(arguments),
				_ => null
			};

			return new ValueTask<TValue>(result is null ? default! : (TValue)result);
		}

		private object? StoreValue(object?[] arguments)
		{
			storage[arguments[0]?.ToString() ?? string.Empty] = arguments[1]?.ToString();
			return null;
		}

		private object? RemoveValue(object?[] arguments)
		{
			storage.Remove(arguments[0]?.ToString() ?? string.Empty);
			return null;
		}
	}

	#pragma warning restore

	#pragma warning disable
	private sealed class RoutedHttpMessageHandler : HttpMessageHandler
	{
		private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> responder;

		public RoutedHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> responder)
		{
			this.responder = responder;
		}

		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			return responder(request);
		}
	}
	#pragma warning restore
}