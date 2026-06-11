using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WileyCoWeb.Api.Configuration;
using WileyCoWeb.Contracts;
using WileyCoWeb.IntegrationTests.Infrastructure;
using WileyWidget.Services;

namespace WileyCoWeb.IntegrationTests;

public sealed class JarvisHealthApiTests : IClassFixture<ApiApplicationFactory>
{
    private readonly ApiApplicationFactory factory;
    private readonly JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web);

    public JarvisHealthApiTests(ApiApplicationFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    [Trait("Category", "HighRisk")]
    public async Task JarvisHealth_ReturnsHealthySnapshot()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/ai/health");
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<JarvisHealthResponse>(jsonOptions);
        Assert.NotNull(payload);
        Assert.Equal("healthy", payload.Status);
    }

    [Fact]
    [Trait("Category", "HighRisk")]
    public async Task JarvisHealth_ReportsDegradedWhenLatestTurnUsedFallback()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();
        var healthState = factory.Services.GetRequiredService<IJarvisHealthState>();
        healthState.RecordTurn("deterministic", usedFallback: true, failureCode: "rate_limited");

        var response = await client.GetAsync("/api/ai/health");
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<JarvisHealthResponse>(jsonOptions);
        Assert.NotNull(payload);
        Assert.Equal("degraded", payload.Status);
        Assert.True(payload.LatestUsedFallback);
    }

    [Fact]
    [Trait("Category", "HighRisk")]
    public async Task JarvisChat_RecordsSemanticKernelAnswerSourceInHealthEndpoint()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();

        var chatRequest = new WorkspaceChatRequest(
            "What is the current break-even rate?",
            "Town of Wiley FY 2026 workspace",
            "Town of Wiley",
            2026)
        {
            ConversationHistory =
            [
                new WorkspaceChatMessage("user", "Prior council question"),
                new WorkspaceChatMessage("assistant", "Prior council answer")
            ]
        };

        var chatResponse = await client.PostAsJsonAsync("/api/ai/chat", chatRequest, jsonOptions);
        Assert.Equal(HttpStatusCode.OK, chatResponse.StatusCode);

        var chatPayload = await chatResponse.Content.ReadFromJsonAsync<WorkspaceChatResponse>(jsonOptions);
        Assert.NotNull(chatPayload);
        Assert.False(string.IsNullOrWhiteSpace(chatPayload.AnswerSource));

        var healthResponse = await client.GetAsync("/api/ai/health");
        healthResponse.EnsureSuccessStatusCode();

        var healthPayload = await healthResponse.Content.ReadFromJsonAsync<JarvisHealthResponse>(jsonOptions);
        Assert.NotNull(healthPayload);
        Assert.Equal(chatPayload.AnswerSource, healthPayload.LatestAnswerSource);
        Assert.Equal(chatPayload.UsedFallback, healthPayload.LatestUsedFallback);
        Assert.False(string.IsNullOrWhiteSpace(healthPayload.LastTurnAtUtc));
    }

    // --- Local-only secret resolution (post-AWS cull; no remote fetches) ---
    [Fact]
    public async Task SecretResolver_ResolvesConfigDirectKey_WithoutRemoteFetch()
    {
        var config = new ConfigurationManager();
        config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["XAI_API_KEY"] = "test-xai-key-from-config"
        });

        var resolver = new SecretResolver(config);
        var result = await resolver.ResolveXaiSecretAsync();

        Assert.NotNull(result);
        Assert.Equal("config:XAI_API_KEY", result.ResolvedKeySource);
        Assert.True(result.ConfigDirectKeyPresent);
        Assert.False(result.SecretFetchAttempted);
        Assert.True(result.ConfigurationInjected);
    }

    [Fact]
    public async Task SecretResolver_ResolvesNamedApiKey_WithoutRemoteFetch()
    {
        var config = new ConfigurationManager();
        config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["XAI:ApiKey"] = "another-test-key-present"
        });

        var resolver = new SecretResolver(config);
        var result = await resolver.ResolveXaiSecretAsync();

        Assert.Equal("config:XAI:ApiKey", result.ResolvedKeySource);
        Assert.True(result.ConfigNamedKeyPresent);
        Assert.False(result.SecretFetchAttempted);
        Assert.True(result.ConfigurationInjected);
    }
}
