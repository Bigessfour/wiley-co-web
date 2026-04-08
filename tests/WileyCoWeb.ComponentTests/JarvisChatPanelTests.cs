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
	public async Task AskJarvis_FirstConversationShowsOnboardingPrompt()
	{
		var state = new WorkspaceState();
		var handler = new RecordingHttpMessageHandler(firstResponseAnswer: "Hi Guest. I’ll keep this thread tied to Water FY 2026. Tell me your preferred name, your role or department, and what you want Jarvis to help with.", firstResponseProfileSummary: "Onboarding pending for Guest: preferred name, role or department, and Jarvis goals have not been captured yet.");

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

		Assert.Contains("preferred name", cut.Markup, StringComparison.OrdinalIgnoreCase);
		Assert.Contains("Profile summary", cut.Markup);
		Assert.Contains("Onboarding pending", cut.Markup);
		Assert.Contains("Jarvis will ask a few setup questions on first contact.", cut.Markup);
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

		Assert.Equal(2, handler.Requests.Count);

		var secondRequest = JsonSerializer.Deserialize<WorkspaceChatRequest>(handler.Requests[1], JsonOptions);
		Assert.NotNull(secondRequest);
		Assert.Equal("What did I just ask you?", secondRequest.Question);
		Assert.NotNull(secondRequest.ConversationHistory);
		Assert.Equal(2, secondRequest.ConversationHistory!.Count);
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
		Assert.Equal(2, handler.Requests.Count);
		Assert.EndsWith("/api/ai/chat/reset", handler.Paths[1], StringComparison.Ordinal);
	}

	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
	{
		PropertyNameCaseInsensitive = true
	};

	private static async Task AskJarvisAsync(IRenderedComponent<JarvisChatPanel> cut, string question)
	{
		var textArea = cut.Find("textarea");
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

		public RecordingHttpMessageHandler(string? firstResponseAnswer = null, string? firstResponseProfileSummary = null)
		{
			this.firstResponseAnswer = firstResponseAnswer ?? "First answer";
			this.firstResponseProfileSummary = firstResponseProfileSummary ?? "Preferences summary";
		}

		public List<string> Requests { get; } = [];
		public List<string> Paths { get; } = [];

		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			_ = cancellationToken;
			Paths.Add(request.RequestUri?.AbsolutePath ?? request.RequestUri?.ToString() ?? string.Empty);
			Requests.Add(await request.Content!.ReadAsStringAsync(cancellationToken));

			if (request.RequestUri is not null && request.RequestUri.AbsolutePath.EndsWith("/reset", StringComparison.OrdinalIgnoreCase))
			{
				return new HttpResponseMessage(HttpStatusCode.NoContent);
			}

			var answer = Requests.Count == 1 ? "First answer" : "Second answer";
			var profileSummary = Requests.Count == 1 ? firstResponseProfileSummary : "Preferences summary";
			var currentAnswer = Requests.Count == 1 ? firstResponseAnswer : answer;
			var response = new WorkspaceChatResponse(
				Requests.Count == 1 ? "What is the current status?" : "What did I just ask you?",
				currentAnswer,
				false,
				"Test context")
			{
				UserDisplayName = "Alex Morgan",
				UserProfileSummary = profileSummary,
				ConversationId = "jarvis:alex-morgan:water-utility:2026",
				ConversationMessageCount = Requests.Count * 2
			};

			return new HttpResponseMessage(HttpStatusCode.OK)
			{
				Content = new StringContent(JsonSerializer.Serialize(response, JsonOptions), Encoding.UTF8, "application/json")
			};
		}
	}
	#pragma warning restore S1144
}