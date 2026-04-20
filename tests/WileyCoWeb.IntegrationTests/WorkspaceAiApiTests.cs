using System.Net;
using System.Text.Json;
using WileyCoWeb.Contracts;
using WileyCoWeb.IntegrationTests.Infrastructure;

namespace WileyCoWeb.IntegrationTests;

public sealed class WorkspaceAiApiTests : IClassFixture<ApiApplicationFactory>
{
    private readonly ApiApplicationFactory factory;
    private readonly JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web);

    public WorkspaceAiApiTests(ApiApplicationFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task PostChat_ReturnsOnboardingResponse_AndUsesHeaderBasedUserContext()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Wiley-User-Id", "user/123");
        client.DefaultRequestHeaders.Add("X-Wiley-User-Name", "Alex Morgan");
        client.DefaultRequestHeaders.Add("X-Wiley-User-Email", "alex@example.com");

        var response = await client.PostAsJsonAsync("/api/ai/chat", new WorkspaceChatRequest(
            "What should I know about the current workspace?",
            "Current workspace context",
            "Water Utility",
            2026), jsonOptions);

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<WorkspaceChatResponse>(jsonOptions);

        Assert.NotNull(payload);
        Assert.True(payload.IsFirstConversation);
        Assert.True(payload.CanResetConversation);
        Assert.Equal("Alex Morgan", payload.UserDisplayName);
        Assert.Equal("jarvis:user-123:water-utility:2026", payload.ConversationId);
        Assert.Contains("preferred name", payload.Answer, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PostChatReset_ClearsConversation_AndNextRequestStartsOnboardingAgain()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Wiley-User-Id", "user/123");
        client.DefaultRequestHeaders.Add("X-Wiley-User-Name", "Alex Morgan");

        var firstResponse = await client.PostAsJsonAsync("/api/ai/chat", new WorkspaceChatRequest(
            "What should I know about the current workspace?",
            "Current workspace context",
            "Water Utility",
            2026), jsonOptions);
        firstResponse.EnsureSuccessStatusCode();

        var resetResponse = await client.PostAsJsonAsync("/api/ai/chat/reset", new WorkspaceConversationResetRequest(
            "Current workspace context",
            "Water Utility",
            2026), jsonOptions);

        Assert.Equal(HttpStatusCode.NoContent, resetResponse.StatusCode);

        var secondResponse = await client.PostAsJsonAsync("/api/ai/chat", new WorkspaceChatRequest(
            "What should I know about the current workspace?",
            "Current workspace context",
            "Water Utility",
            2026), jsonOptions);
        secondResponse.EnsureSuccessStatusCode();
        var payload = await secondResponse.Content.ReadFromJsonAsync<WorkspaceChatResponse>(jsonOptions);

        Assert.NotNull(payload);
        Assert.True(payload.IsFirstConversation);
        Assert.Contains("preferred name", payload.Answer, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PostChat_ReturnsBadRequest_WhenQuestionIsMissing()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/ai/chat", new WorkspaceChatRequest(
            string.Empty,
            "Current workspace context",
            "Water Utility",
            2026), jsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetRecommendationHistory_ReturnsPersistedItems_ForCurrentUserScope()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Wiley-User-Id", "user/123");
        client.DefaultRequestHeaders.Add("X-Wiley-User-Name", "Alex Morgan");

        var chatResponse = await client.PostAsJsonAsync("/api/ai/chat", new WorkspaceChatRequest(
            "What should I know about the current workspace?",
            "Current workspace context",
            "Water Utility",
            2026), jsonOptions);
        chatResponse.EnsureSuccessStatusCode();

        var payload = await WaitForHistoryPayloadAsync(client);
        Assert.NotNull(payload);
        Assert.NotEmpty(payload.Items);
        Assert.All(payload.Items, item => Assert.Equal("Alex Morgan", item.UserDisplayName));
    }

    [Fact]
    public async Task PostWorkspaceNavigation_ReturnsNoContent_ForTelemetryPayload()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/workspace/navigation",
            new WorkspaceNavigationClickRequest(
                "break-even",
                "/wiley-workspace/break-even",
                "overview",
                "Water Utility",
                2026,
                false,
                false,
                DateTimeOffset.UtcNow.ToString("O")),
            jsonOptions);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private static async Task<WorkspaceRecommendationHistoryResponse?> WaitForHistoryPayloadAsync(HttpClient client, int timeoutMilliseconds = 2000, int pollMilliseconds = 50)
    {
        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMilliseconds);

        while (DateTime.UtcNow < deadline)
        {
            var response = await client.GetAsync("/api/ai/recommendations?enterprise=Water%20Utility&fiscalYear=2026&limit=5");
            if (response.IsSuccessStatusCode)
            {
                var payload = await response.Content.ReadFromJsonAsync<WorkspaceRecommendationHistoryResponse>(jsonOptions);
                if (payload?.Items.Count > 0)
                {
                    return payload;
                }
            }

            await Task.Delay(pollMilliseconds);
        }

        var finalResponse = await client.GetAsync("/api/ai/recommendations?enterprise=Water%20Utility&fiscalYear=2026&limit=5");
        return await finalResponse.Content.ReadFromJsonAsync<WorkspaceRecommendationHistoryResponse>(jsonOptions);
    }
}