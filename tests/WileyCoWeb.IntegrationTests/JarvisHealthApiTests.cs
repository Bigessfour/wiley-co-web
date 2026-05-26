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

    // --- SSM resolution tests (minimal, non-HighRisk; verify skip + param parsing without AWS calls or secrets) ---
    [Fact]
    public async Task SecretResolver_SkipsSsmFetch_WhenEnvOrConfigKeyPresent_EvenIfSsmParameterNameConfigured()
    {
        var config = new ConfigurationManager();
        config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["XAI_API_KEY"] = "test-xai-key-from-config-for-ssm-skip-test",
            ["XAI:ParameterName"] = "/wiley-widget/xai-api-key",
            ["XAI:SSMParameterName"] = "/should-be-ignored-when-key-present"
        });

        var resolver = new SecretResolver(config);
        var result = await resolver.ResolveXaiSecretAsync();

        Assert.NotNull(result);
        Assert.True(result.ResolvedKeySource.StartsWith("env:") || result.ResolvedKeySource.StartsWith("config:"), $"Expected env or config source when key present, got {result.ResolvedKeySource}");
        Assert.False(result.SsmFetchAttempted, "SSM must be skipped when a key is already present in env/config (per requirements).");
        Assert.Equal("not-attempted", result.SsmFetchStatus);
        // Param name still parsed from config for logging/ops visibility.
        Assert.Equal("/wiley-widget/xai-api-key", result.SsmParameterName);
    }

    [Fact]
    public async Task SecretResolver_ParsesSsmParameterName_FromAllSupportedConfigKeys()
    {
        var config = new ConfigurationManager();
        config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["XAI:ApiKey"] = "another-test-key-present",
            ["XAI:ParameterName"] = "/custom/path/to/xai"
        });

        var resolver = new SecretResolver(config);
        var result = await resolver.ResolveXaiSecretAsync();

        Assert.Equal("/custom/path/to/xai", result.SsmParameterName);
        Assert.False(result.SsmFetchAttempted);
    }
}
