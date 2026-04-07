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
		public List<string> Requests { get; } = [];

		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			_ = cancellationToken;
			Requests.Add(await request.Content!.ReadAsStringAsync(cancellationToken));

			var answer = Requests.Count == 1 ? "First answer" : "Second answer";
			var response = new WorkspaceChatResponse(
				Requests.Count == 1 ? "What is the current status?" : "What did I just ask you?",
				answer,
				false,
				"Test context")
			{
				UserDisplayName = "Alex Morgan",
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