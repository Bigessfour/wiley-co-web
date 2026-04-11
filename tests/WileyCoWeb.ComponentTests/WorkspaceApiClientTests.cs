using System.Net;
using System.Text;
using System.Text.Json;
using WileyCoWeb.Contracts;
using WileyCoWeb.Services;

namespace WileyCoWeb.ComponentTests;

public sealed class WorkspaceApiClientTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task WorkspaceSnapshotApiService_GetScenariosAsync_ReturnsCollectionFromApi()
    {
        var client = new HttpClient(new RoutedHttpMessageHandler(request =>
        {
            Assert.Contains("enterprise=Water%20Utility", request.RequestUri?.Query);
            Assert.Contains("fiscalYear=2026", request.RequestUri?.Query);

            var payload = new WorkspaceScenarioCollectionResponse(
            [
                new WorkspaceScenarioSummaryResponse(7, "Council Review", "Water Utility", 2026, "2026-04-05T12:00:00Z", 55.25m, 13250m, 240m, 6200m, 1, "Scenario persisted from the workspace shell.")
            ]);

            return JsonResponse(payload);
        }))
        {
            BaseAddress = new Uri("https://example.test/")
        };

        var service = new WorkspaceSnapshotApiService(client);
        var response = await service.GetScenariosAsync("Water Utility", 2026);

        Assert.Single(response.Scenarios);
        Assert.Equal("Council Review", response.Scenarios[0].ScenarioName);
    }

    [Fact]
    public async Task WorkspaceSnapshotApiService_SaveScenarioAsync_PostsScenarioAndParsesResponse()
    {
        string? postedJson = null;
        var client = new HttpClient(new RoutedHttpMessageHandler(async request =>
        {
            postedJson = await request.Content!.ReadAsStringAsync();
            return JsonResponse(new WorkspaceScenarioSummaryResponse(9, "Council Review", "Water Utility", 2026, "2026-04-05T12:00:00Z", 55.25m, 13250m, 240m, 6200m, 1, "Saved"));
        }))
        {
            BaseAddress = new Uri("https://example.test/")
        };

        var request = new WorkspaceScenarioSaveRequest(
            "Council Review",
            "Saved",
            new WorkspaceBootstrapData("Water Utility", 2026, "Council Review", 55.25m, 13250m, 240m, "2026-04-05T12:00:00Z"));

        var service = new WorkspaceSnapshotApiService(client);
        var response = await service.SaveScenarioAsync(request);

        Assert.Contains("Council Review", postedJson!, StringComparison.Ordinal);
        Assert.Equal(9, response.SnapshotId);
        Assert.Equal("Saved", response.Description);
    }

    [Fact]
    public async Task WorkspaceSnapshotApiService_SaveWorkspaceBaselineAsync_PutsBaselineAndParsesResponse()
    {
        HttpMethod? method = null;
        var client = new HttpClient(new RoutedHttpMessageHandler(request =>
        {
            method = request.Method;
            return JsonResponse(new WorkspaceBaselineUpdateResponse(
                "Water Utility",
                2026,
                "2026-04-05T12:00:00Z",
                "Saved baseline values for Water Utility FY 2026.",
                new WorkspaceBootstrapData("Water Utility", 2026, "Base Planning Scenario", 61.75m, 15500m, 275m, "2026-04-05T12:00:00Z")));
        }))
        {
            BaseAddress = new Uri("https://example.test/")
        };

        var service = new WorkspaceSnapshotApiService(client);
        var response = await service.SaveWorkspaceBaselineAsync(new WorkspaceBaselineUpdateRequest("Water Utility", 2026, 61.75m, 15500m, 275m));

        Assert.Equal(HttpMethod.Put, method);
        Assert.Equal(61.75m, response.Snapshot.CurrentRate);
    }

    [Fact]
    public async Task WorkspaceSnapshotApiService_GetScenarioSnapshotAsync_ReturnsBootstrapPayload()
    {
        var client = new HttpClient(new RoutedHttpMessageHandler(_ => JsonResponse(
            new WorkspaceBootstrapData("Water Utility", 2026, "Council Review", 55.25m, 13250m, 240m, "2026-04-05T12:00:00Z"))))
        {
            BaseAddress = new Uri("https://example.test/")
        };

        var service = new WorkspaceSnapshotApiService(client);
        var response = await service.GetScenarioSnapshotAsync(7);

        Assert.Equal("Council Review", response.ActiveScenarioName);
    }

    [Fact]
    public async Task WorkspaceAiApiService_AskAsync_PostsRequestAndReturnsResponse()
    {
        string? postedJson = null;
        var client = new HttpClient(new RoutedHttpMessageHandler(async request =>
        {
            postedJson = await request.Content!.ReadAsStringAsync();
            return JsonResponse(new WorkspaceChatResponse("Question", "Answer", false, "Context")
            {
                ConversationId = "conv-42",
                ConversationMessageCount = 3,
                UserDisplayName = "Alex Morgan"
            });
        }))
        {
            BaseAddress = new Uri("https://example.test/")
        };

        var service = new WorkspaceAiApiService(client);
        var response = await service.AskAsync(new WorkspaceChatRequest("Question", "Context", "Water Utility", 2026));

        Assert.Contains("Water Utility", postedJson!, StringComparison.Ordinal);
        Assert.Equal("Answer", response.Answer);
        Assert.Equal("conv-42", response.ConversationId);
    }

    [Fact]
    public async Task WorkspaceAiApiService_ResetConversationAsync_ThrowsForFailureStatus()
    {
        var client = new HttpClient(new RoutedHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("reset rejected", Encoding.UTF8, "text/plain")
        }))
        {
            BaseAddress = new Uri("https://example.test/")
        };

        var service = new WorkspaceAiApiService(client);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ResetConversationAsync(new WorkspaceConversationResetRequest("Context", "Water Utility", 2026)));

        Assert.Contains("reset rejected", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WorkspaceAiApiService_GetRecommendationHistoryAsync_ReturnsItems()
    {
        var client = new HttpClient(new RoutedHttpMessageHandler(request =>
        {
            Assert.Contains("enterprise=Water%20Utility", request.RequestUri?.Query, StringComparison.Ordinal);
            Assert.Contains("fiscalYear=2026", request.RequestUri?.Query, StringComparison.Ordinal);

            return JsonResponse(new WorkspaceRecommendationHistoryResponse([
                new WorkspaceRecommendationHistoryItem(
                    "reco:123",
                    "jarvis:user-123:water-utility:2026",
                    "Alex Morgan",
                    "What changed?",
                    "Review rate delta before publishing.",
                    true,
                    "2026-04-10T12:00:00.0000000Z")
            ]));
        }))
        {
            BaseAddress = new Uri("https://example.test/")
        };

        var service = new WorkspaceAiApiService(client);
        var response = await service.GetRecommendationHistoryAsync(new WorkspaceRecommendationHistoryRequest("Water Utility", 2026, 8));

        Assert.Single(response.Items);
        Assert.Equal("Alex Morgan", response.Items[0].UserDisplayName);
    }

    private static HttpResponseMessage JsonResponse<T>(T payload)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json")
        };
    }

    private sealed class RoutedHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> responder;

        public RoutedHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
            : this(request => Task.FromResult(responder(request)))
        {
        }

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
}