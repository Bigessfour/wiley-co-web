using System.Net;
using System.Text;
using System.Text.Json;
using Bunit;
using Bunit.JSInterop;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Syncfusion.Blazor;
using WileyCoWeb.Components;
using WileyCoWeb.Contracts;
using WileyCoWeb.Services;
using WileyCoWeb.State;

namespace WileyCoWeb.ComponentTests;

public sealed class JarvisChatPanelTests : TestContext
{
	[Fact]
	public async Task AskJarvis_FirstConversationUsesLiveAnswerBeforeOnboardingFallback()
	{
		var state = new WorkspaceState();
		var handler = new RecordingHttpMessageHandler(firstResponseAnswer: "Live answer", firstResponseProfileSummary: "Preferences summary");

		Services.AddSingleton(state);
		Services.AddSingleton(new WorkspaceAiApiService(new HttpClient(handler)
		{
			BaseAddress = new Uri("https://workspace.local/")
		}));
		Services.AddSyncfusionBlazor();

		SetRendererInfo(new RendererInfo("Server", true));
		JSInterop.Mode = JSRuntimeMode.Loose;

		var cut = RenderComponent<JarvisChatPanel>();

		await AskJarvisAsync(cut, "What should I know about the current workspace?");

		Assert.Contains("jarvis-chat-ui", cut.Markup, StringComparison.OrdinalIgnoreCase);
		Assert.Single(handler.ChatRequests);
		Assert.Contains("Live answer", cut.Markup);
		Assert.Contains("Profile summary", cut.Markup);
		Assert.Contains("Conversation jarvis:alex-morgan:water-utility:2026", cut.Markup);
		Assert.DoesNotContain("preferred name", cut.Markup, StringComparison.OrdinalIgnoreCase);
	}

	[Fact]
	public async Task AskJarvis_PreservesConversationHistoryAcrossTurns()
	{
		var state = new WorkspaceState();
		var handler = new RecordingHttpMessageHandler();

		Services.AddSingleton(state);
		Services.AddSingleton(new WorkspaceAiApiService(new HttpClient(handler)
		{
			BaseAddress = new Uri("https://workspace.local/")
		}));
		Services.AddSyncfusionBlazor();

		SetRendererInfo(new RendererInfo("Server", true));
		JSInterop.Mode = JSRuntimeMode.Loose;

		var cut = RenderComponent<JarvisChatPanel>();

		await AskJarvisAsync(cut, "What is the current status?");
		await AskJarvisAsync(cut, "What did I just ask you?");

		Assert.True(handler.ChatRequests.Count >= 2);

		var secondRequest = JsonSerializer.Deserialize<WorkspaceChatRequest>(handler.ChatRequests[1], JsonOptions);
		Assert.NotNull(secondRequest);
		Assert.Equal("What did I just ask you?", secondRequest.Question);
		Assert.NotNull(secondRequest.ConversationHistory);
		Assert.True(secondRequest.ConversationHistory!.Count >= 2);
		Assert.Equal("user", secondRequest.ConversationHistory[0].Role);
		Assert.Equal("What is the current status?", secondRequest.ConversationHistory[0].Content);
		Assert.Equal("assistant", secondRequest.ConversationHistory[1].Role);
		Assert.Contains("First answer", secondRequest.ConversationHistory[1].Content);

		Assert.Contains("Conversation history", cut.Markup);
		Assert.Contains("What is the current status?", cut.Markup);
		Assert.Contains("What did I just ask you?", cut.Markup);
		Assert.Contains("First answer", cut.Markup);
		Assert.Contains("Second answer", cut.Markup);
	}

	[Fact]
	public async Task ResetThread_ClearsConversationHistoryAndRequestsBackendReset()
	{
		var state = new WorkspaceState();
		var handler = new RecordingHttpMessageHandler();

		Services.AddSingleton(state);
		Services.AddSingleton(new WorkspaceAiApiService(new HttpClient(handler)
		{
			BaseAddress = new Uri("https://workspace.local/")
		}));
		Services.AddSyncfusionBlazor();

		SetRendererInfo(new RendererInfo("Server", true));
		JSInterop.Mode = JSRuntimeMode.Loose;

		var cut = RenderComponent<JarvisChatPanel>();

		await AskJarvisAsync(cut, "What is the current status?");

		var resetButton = cut.FindAll("button").First(button => button.TextContent.Contains("Reset Thread", StringComparison.Ordinal));
		await resetButton.ClickAsync(new MouseEventArgs());

		cut.WaitForAssertion(() => Assert.DoesNotContain("What is the current status?", cut.Markup));
		Assert.Contains("Jarvis thread reset for the current workspace context.", cut.Markup);
		Assert.Contains(handler.Paths, path => path.EndsWith("/api/ai/chat/reset", StringComparison.Ordinal));
	}

	[Fact]
	public async Task AskJarvis_FallbackResponse_ShowsRuntimeUnavailableBanner()
	{
		var state = new WorkspaceState();
		var handler = new RecordingHttpMessageHandler(firstResponseUsedFallback: true);

		Services.AddSingleton(state);
		Services.AddSingleton(new WorkspaceAiApiService(new HttpClient(handler)
		{
			BaseAddress = new Uri("https://workspace.local/")
		}));
		Services.AddSyncfusionBlazor();

		SetRendererInfo(new RendererInfo("Server", true));
		JSInterop.Mode = JSRuntimeMode.Loose;

		var cut = RenderComponent<JarvisChatPanel>();

		await AskJarvisAsync(cut, "Summarize the current scenario pressure.");

		cut.WaitForAssertion(() =>
		{
			Assert.Contains("Jarvis runtime", cut.Markup);
			Assert.Contains("AI runtime unavailable", cut.Markup);
			Assert.Contains("fallback guidance", cut.Markup, StringComparison.OrdinalIgnoreCase);
		});
	}

	[Fact]
	public void JarvisChatPanel_DoesNotRequestRecommendationHistoryBeforeWorkspaceBootstrap()
	{
		var state = new WorkspaceState();
		var handler = new RecordingHttpMessageHandler();

		Services.AddSingleton(state);
		Services.AddSingleton(new WorkspaceAiApiService(new HttpClient(handler)
		{
			BaseAddress = new Uri("https://workspace.local/")
		}));
		Services.AddSyncfusionBlazor();

		SetRendererInfo(new RendererInfo("Server", true));
		JSInterop.Mode = JSRuntimeMode.Loose;

		var cut = RenderComponent<JarvisChatPanel>();

		cut.WaitForAssertion(() => Assert.Contains("Recommendation history will load after an enterprise and fiscal year are available.", cut.Markup));
		Assert.DoesNotContain(handler.Paths, path => path.EndsWith("/api/ai/recommendations", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public void JarvisChatPanel_LoadsRecommendationHistoryAfterWorkspaceBootstrap()
	{
		var state = new WorkspaceState();
		var handler = new RecordingHttpMessageHandler();

		Services.AddSingleton(state);
		Services.AddSingleton(new WorkspaceAiApiService(new HttpClient(handler)
		{
			BaseAddress = new Uri("https://workspace.local/")
		}));
		Services.AddSyncfusionBlazor();

		SetRendererInfo(new RendererInfo("Server", true));
		JSInterop.Mode = JSRuntimeMode.Loose;

		var cut = RenderComponent<JarvisChatPanel>();

		state.ApplyBootstrap(new WorkspaceBootstrapData(
			"Water Utility",
			2026,
			"Council Review",
			45m,
			30000m,
			120m,
			DateTime.UtcNow.ToString("O")));

		cut.WaitForAssertion(() =>
		{
			Assert.Contains(handler.Paths, path => path.EndsWith("/api/ai/recommendations", StringComparison.OrdinalIgnoreCase));
			Assert.Contains("No saved recommendations yet for this workspace scope.", cut.Markup);
		});
	}

	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
	{
		PropertyNameCaseInsensitive = true
	};

	private static async Task AskJarvisAsync(IRenderedComponent<JarvisChatPanel> cut, string question)
	{
		var textArea = cut.Find("#jarvis-question-input");
		textArea.Change(question);

		var askButton = cut.FindAll("button").First(button => button.TextContent.Contains("Ask Jarvis", StringComparison.Ordinal));
		await askButton.ClickAsync(new MouseEventArgs());

		cut.WaitForAssertion(() => Assert.Contains(question, cut.Markup));
	}

	#pragma warning disable S1144
	private sealed class RecordingHttpMessageHandler : HttpMessageHandler
	{
		private readonly string firstResponseAnswer;
		private readonly string firstResponseProfileSummary;
		private readonly bool firstResponseUsedFallback;
		private int chatCallCount;

		public RecordingHttpMessageHandler(string? firstResponseAnswer = null, string? firstResponseProfileSummary = null, bool firstResponseUsedFallback = false)
		{
			this.firstResponseAnswer = firstResponseAnswer ?? "First answer";
			this.firstResponseProfileSummary = firstResponseProfileSummary ?? "Preferences summary";
			this.firstResponseUsedFallback = firstResponseUsedFallback;
		}

		public List<string> Requests { get; } = [];
		public List<string> ChatRequests { get; } = [];
		public List<string> Paths { get; } = [];

		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			_ = cancellationToken;
			var path = request.RequestUri?.AbsolutePath ?? request.RequestUri?.ToString() ?? string.Empty;
			Paths.Add(path);

			if (request.Method == HttpMethod.Get && path.EndsWith("/api/ai/recommendations", StringComparison.OrdinalIgnoreCase))
			{
				return new HttpResponseMessage(HttpStatusCode.OK)
				{
					Content = new StringContent(JsonSerializer.Serialize(new WorkspaceRecommendationHistoryResponse([]), JsonOptions), Encoding.UTF8, "application/json")
				};
			}

			var requestBody = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
			Requests.Add(requestBody);

			if (request.RequestUri is not null && request.RequestUri.AbsolutePath.EndsWith("/reset", StringComparison.OrdinalIgnoreCase))
			{
				return new HttpResponseMessage(HttpStatusCode.NoContent);
			}

			if (!path.EndsWith("/api/ai/chat", StringComparison.OrdinalIgnoreCase))
			{
				return new HttpResponseMessage(HttpStatusCode.NotFound);
			}

			chatCallCount++;
			ChatRequests.Add(requestBody);

			var answer = chatCallCount == 1 ? "First answer" : "Second answer";
			var profileSummary = chatCallCount == 1 ? firstResponseProfileSummary : "Preferences summary";
			var currentAnswer = chatCallCount == 1 ? firstResponseAnswer : answer;
			var response = new WorkspaceChatResponse(
				chatCallCount == 1 ? "What is the current status?" : "What did I just ask you?",
				currentAnswer,
				chatCallCount == 1 ? firstResponseUsedFallback : false,
				"Test context")
			{
				UserDisplayName = "Alex Morgan",
				UserProfileSummary = profileSummary,
				ConversationId = "jarvis:alex-morgan:water-utility:2026",
				ConversationMessageCount = chatCallCount * 2
			};

			return new HttpResponseMessage(HttpStatusCode.OK)
			{
				Content = new StringContent(JsonSerializer.Serialize(response, JsonOptions), Encoding.UTF8, "application/json")
			};
		}
	}
	#pragma warning restore S1144
}