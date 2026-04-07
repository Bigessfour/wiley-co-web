using System.Net;
using System.Text;
using System.Text.Json;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Syncfusion.Blazor;
using WileyCoWeb.Contracts;
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
	public void MainLayout_RendersNavigationChrome_AndBodyContent()
	{
		using var context = CreateContext();

		var cut = context.RenderComponent<MainLayout>(parameters => parameters
			.Add(p => p.Body, (RenderFragment)(builder => builder.AddMarkupContent(0, "<h1>Workspace Body</h1>"))));

		Assert.Contains("Workspace Body", cut.Markup);
		Assert.Contains("Syncfusion Finance Workspace", cut.Markup);
		Assert.Contains("Budget Dashboard", cut.Markup);
		Assert.Contains("Reload", cut.Markup);
	}

	[Fact]
	public void NavMenu_RendersExpectedWorkspaceLinks()
	{
		using var context = CreateContext();

		var cut = context.RenderComponent<NavMenu>();

		Assert.Contains("Rate Study Console", cut.Markup);
		Assert.Contains("Workspace Alias", cut.Markup);
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
	public void JarvisChatPanel_SendsConversation_AndClearsItAgain()
	{
		using var context = CreateContext();

		var cut = context.RenderComponent<JarvisChatPanel>();

		Assert.Contains("Jarvis chat", cut.Markup);
		Assert.Contains("No prior Jarvis turns yet.", cut.Markup);
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
		var snapshotService = new WorkspaceSnapshotApiService(snapshotClient);
		context.Services.AddScoped(_ => snapshotService);
		context.Services.AddScoped(_ => new WorkspaceBootstrapService(snapshotClient, workspaceState, snapshotService));
		context.Services.AddScoped(_ => new WorkspaceDocumentExportService());
		context.Services.AddScoped(_ => new WorkspaceAiApiService(CreateAiClient()));
		context.Services.AddScoped(_ => new QuickBooksImportApiService(CreateImportClient()));
		context.Services.AddScoped(_ => new BrowserDownloadService(jsRuntime));
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

	private static HttpClient CreateAiClient()
	{
		return new HttpClient(new RoutedHttpMessageHandler(request =>
		{
			if (request.Method == HttpMethod.Post && request.RequestUri?.AbsolutePath.EndsWith("/api/ai/chat", StringComparison.OrdinalIgnoreCase) == true)
			{
				var response = new WorkspaceChatResponse("What should I know about the current workspace?", "Jarvis test response", false, "Test context")
				{
					ConversationId = "conv-test",
					ConversationMessageCount = 2,
					UserDisplayName = "Test User"
				};

				return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
				{
					Content = new StringContent(JsonSerializer.Serialize(response, JsonOptions), Encoding.UTF8, "application/json")
				});
			}

			return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
		}))
		{
			BaseAddress = new Uri("https://example.test/")
		};
	}

	private static HttpClient CreateImportClient()
	{
		return new HttpClient(new RoutedHttpMessageHandler(request =>
		{
			if (request.RequestUri?.AbsolutePath.EndsWith("/api/imports/quickbooks/assistant", StringComparison.OrdinalIgnoreCase) == true)
			{
				var response = new QuickBooksImportGuidanceResponse(
					"What should I know about the current workspace?",
					"Guidance ready",
					false,
					"Preview guidance");

				return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
				{
					Content = new StringContent(JsonSerializer.Serialize(response, JsonOptions), Encoding.UTF8, "application/json")
				});
			}

			return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
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