using System.Text.Json;
using System.Text;
using System.Net;
using Microsoft.Extensions.Configuration;
using WileyCoWeb.Contracts;
using WileyWidget.Services;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Tests;

[CollectionDefinition("WorkspaceAiAssistantService environment", DisableParallelization = true)]
public sealed class WorkspaceAiAssistantServiceEnvironmentCollection : ICollectionFixture<WorkspaceAiAssistantServiceEnvironmentFixture>
{
}

public sealed class WorkspaceAiAssistantServiceEnvironmentFixture : IDisposable
{
    private readonly string? previousApiKey = Environment.GetEnvironmentVariable("XAI_API_KEY");

    public WorkspaceAiAssistantServiceEnvironmentFixture()
    {
        Environment.SetEnvironmentVariable("XAI_API_KEY", null);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("XAI_API_KEY", previousApiKey);
    }
}

[Collection("WorkspaceAiAssistantService environment")]
public sealed class WorkspaceAiAssistantServiceTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public void SemanticKernelConnector_UsesDocumentedXaiDefaultsAndAutoFunctionChoice()
    {
        var defaultConfiguration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var defaultAiConfiguration = WorkspaceAiKernelFactory.ResolveConfiguration(defaultConfiguration);

        Assert.Equal(WileyWidget.Services.Abstractions.WorkspaceAiKernelFactory.DefaultSemanticKernelModel, defaultAiConfiguration.ResolveModelOrDefault(WorkspaceAiAssistantService.GetDefaultSemanticKernelModel()));
        Assert.Equal("https://api.x.ai/v1", defaultAiConfiguration.ChatCompletionEndpoint.ToString());
        Assert.Equal("https://api.x.ai/v1/responses", defaultAiConfiguration.LegacyResponsesEndpoint.ToString());
        Assert.False(defaultAiConfiguration.StoreResponses);

        var configuredConfiguration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["XaiApiEndpoint"] = "https://alias.example/v1",
            ["XaiModel"] = "grok-4.20-0309-reasoning",
            ["XAI:ChatEndpoint"] = "https://proxy.example/v1"
        }).Build();
        var configuredAiConfiguration = WorkspaceAiKernelFactory.ResolveConfiguration(configuredConfiguration);

        Assert.Equal("grok-4.20-0309-reasoning", configuredAiConfiguration.ResolveModelOrDefault(WorkspaceAiAssistantService.GetDefaultSemanticKernelModel()));
        Assert.Equal("https://proxy.example/v1", configuredAiConfiguration.ChatCompletionEndpoint.ToString());

        var executionSettings = WorkspaceAiAssistantService.CreateSemanticKernelExecutionSettings();
        var functionChoiceBehavior = executionSettings.FunctionChoiceBehavior;

        Assert.NotNull(functionChoiceBehavior);
        Assert.Contains("Auto", functionChoiceBehavior!.GetType().Name, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("https://proxy.example/v1/chat/completions", "https://proxy.example/v1")]
    [InlineData("https://proxy.example/v1/responses", "https://proxy.example/v1")]
    [InlineData("https://proxy.example/v1", "https://proxy.example/v1")]
    public void SemanticKernelConnector_NormalizesCustomChatEndpoints_ToDocumentedBaseUrl(string configuredEndpoint, string expectedEndpoint)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["XAI:ChatEndpoint"] = configuredEndpoint
        }).Build();

        var aiConfiguration = WorkspaceAiKernelFactory.ResolveConfiguration(configuration);

        Assert.Equal(expectedEndpoint, aiConfiguration.ChatCompletionEndpoint.ToString());
    }

    [Fact]
    public async Task AskAsync_FirstConversation_UsesOnboardingAndScopesConversationByUserAndEnterprise()
    {
        var repository = new RecordingConversationRepository();
        var userContext = new TestUserContext("user/123", "Alex Morgan", "alex@example.com");
        var service = CreateService(userContext, repository);

        var response = await service.AskAsync(new WorkspaceChatRequest(
            "What should I know about the current workspace?",
            "Current workspace context",
            "Water Utility",
            2026));

        Assert.True(response.IsFirstConversation);
        Assert.True(response.CanResetConversation);
        Assert.True(response.UsedFallback);
        Assert.Equal("jarvis:user-123:water-utility:2026", response.ConversationId);
        Assert.Equal("Alex Morgan", response.UserDisplayName);
        Assert.Contains("preferred name", response.Answer, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Onboarding pending for Alex Morgan", response.UserProfileSummary);
        await WaitForConditionAsync(() => repository.SavedConversations.Count == 1 && repository.SavedRecommendations.Count == 1);
        Assert.Single(repository.SavedConversations);
        Assert.Single(repository.SavedRecommendations);
        Assert.Equal(2, repository.SavedConversations[0].MessageCount);
    }

    [Fact]
    public async Task AskAsync_SecondConversation_ReusesPersistedHistoryAndAppendsTurn()
    {
        var repository = new RecordingConversationRepository();
        var userContext = new TestUserContext("user/123", "Alex Morgan", "alex@example.com");
        var service = CreateService(userContext, repository);

        await service.AskAsync(new WorkspaceChatRequest(
            "What is the current status?",
            "Current workspace context",
            "Water Utility",
            2026));

        await WaitForConditionAsync(() => repository.SavedConversations.Count == 1 && repository.SavedRecommendations.Count == 1);

        var secondResponse = await service.AskAsync(new WorkspaceChatRequest(
            "What did I just ask you?",
            "Current workspace context",
            "Water Utility",
            2026));

        Assert.False(secondResponse.IsFirstConversation);
        Assert.True(secondResponse.UsedFallback);
        Assert.Equal(4, secondResponse.ConversationMessageCount);
        Assert.Equal("jarvis:user-123:water-utility:2026", secondResponse.ConversationId);
        Assert.Contains("fallback mode is active", secondResponse.Answer, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Runtime diagnostics", secondResponse.Answer, StringComparison.OrdinalIgnoreCase);

        Assert.Equal(2, repository.SavedConversations.Count);

        var persisted = repository.SavedConversations[^1];
        var messages = JsonSerializer.Deserialize<List<WorkspaceChatMessage>>(persisted.MessagesJson, JsonOptions);

        Assert.NotNull(messages);
        Assert.Equal(4, messages!.Count);
        Assert.Equal("user", messages[0].Role);
        Assert.Equal("What is the current status?", messages[0].Content);
        Assert.Equal("assistant", messages[1].Role);
        Assert.Contains("preferred name", messages[1].Content, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("user", messages[2].Role);
        Assert.Equal("What did I just ask you?", messages[2].Content);
        Assert.Equal("assistant", messages[3].Role);
    }

    [Fact]
    public async Task ResetConversationAsync_DeletesScopedConversationThread()
    {
        var repository = new RecordingConversationRepository();
        var userContext = new TestUserContext("user/123", "Alex Morgan", "alex@example.com");
        var service = CreateService(userContext, repository);

        await service.AskAsync(new WorkspaceChatRequest(
            "What should I know about the current workspace?",
            "Current workspace context",
            "Water Utility",
            2026));

        await service.ResetConversationAsync(new WorkspaceConversationResetRequest(
            "Current workspace context",
            "Water Utility",
            2026));

        Assert.Contains("jarvis:user-123:water-utility:2026", repository.DeletedConversationIds);
        Assert.Contains("jarvis:user-123:water-utility:2026", repository.DeletedRecommendationConversationIds);
        Assert.Empty(repository.StoredConversations);
    }

    [Fact]
    public async Task AskAsync_UsesSessionHistory_WhenPersistedConversationIsMissing()
    {
        var repository = new RecordingConversationRepository();
        var userContext = new TestUserContext("user/123", "Alex Morgan", "alex@example.com");
        var service = CreateService(userContext, repository);

        var response = await service.AskAsync(new WorkspaceChatRequest(
            "What time is it?",
            "Current workspace context",
            "Water Utility",
            2026)
        {
            ConversationHistory =
            [
                new WorkspaceChatMessage(" user ", " Prior question "),
                new WorkspaceChatMessage(" assistant ", " Prior answer ")
            ]
        });

        Assert.False(response.IsFirstConversation);
        Assert.True(response.UsedFallback);
        Assert.Equal(4, response.ConversationMessageCount);

        var persisted = repository.SavedConversations[^1];
        var messages = JsonSerializer.Deserialize<List<WorkspaceChatMessage>>(persisted.MessagesJson, JsonOptions);
        Assert.NotNull(messages);
        Assert.Equal("user", messages![0].Role);
        Assert.Equal("Prior question", messages[0].Content);
        Assert.Contains("fallback mode", response.Answer, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("What time is it right now?", "current workspace context")]
    [InlineData("Tell me about the codebase plugin inventory", "codebase insight plugin")]
    [InlineData("Do we have an anomaly variance issue?", "anomaly detection plugin")]
    public async Task AskAsync_UsesQuestionSpecificFallbackBranches(string question, string expectedFragment)
    {
        var repository = new RecordingConversationRepository();
        repository.StoredConversations["jarvis:user-123:water-utility:2026"] = new ConversationHistory
        {
            ConversationId = "jarvis:user-123:water-utility:2026",
            MessagesJson = JsonSerializer.Serialize(new List<WorkspaceChatMessage>
            {
                new("user", "Earlier question"),
                new("assistant", "Earlier answer")
            }, JsonOptions),
            MessageCount = 2,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var service = CreateService(new TestUserContext("user/123", "Alex Morgan", "alex@example.com"), repository);

        var response = await service.AskAsync(new WorkspaceChatRequest(question, "Current workspace context", "Water Utility", 2026));

        Assert.Contains(expectedFragment, response.Answer, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Runtime diagnostics", response.Answer, StringComparison.OrdinalIgnoreCase);
        Assert.False(response.IsFirstConversation);
    }

    [Fact]
    public async Task AskAsync_IgnoresInvalidPersistedConversationJson_AndFallsBackToCurrentRequestHistory()
    {
        var repository = new RecordingConversationRepository();
        repository.StoredConversations["jarvis:user-123:water-utility:2026"] = new ConversationHistory
        {
            ConversationId = "jarvis:user-123:water-utility:2026",
            MessagesJson = "not-json",
            MessageCount = 99,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var service = CreateService(new TestUserContext("user/123", "Alex Morgan", "alex@example.com"), repository);
        var response = await service.AskAsync(new WorkspaceChatRequest(
            "What is the current status?",
            "Current workspace context",
            "Water Utility",
            2026)
        {
            ConversationHistory =
            [
                new WorkspaceChatMessage("user", "Question from request history"),
                new WorkspaceChatMessage("assistant", "Answer from request history")
            ]
        });

        Assert.False(response.IsFirstConversation);
        Assert.Equal(4, response.ConversationMessageCount);

        var persisted = repository.SavedConversations[^1];
        var messages = JsonSerializer.Deserialize<List<WorkspaceChatMessage>>(persisted.MessagesJson, JsonOptions);
        Assert.NotNull(messages);
        Assert.Contains(messages!, message => message.Content == "Question from request history");
    }

    [Fact]
    public async Task AskAsync_WhenLiveKernelFails_UsesLegacyXaiResponse()
    {
        var repository = new RecordingConversationRepository();
        SeedPersistedConversation(repository);
        var httpFactory = new TestHttpClientFactory(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"output\":[{\"content\":[{\"text\":\"Legacy fallback answer with live workspace numbers.\"}]}]}", Encoding.UTF8, "application/json")
        });

        var service = CreateService(
            new TestUserContext("user/123", "Alex Morgan", "alex@example.com"),
            repository,
            settings: new Dictionary<string, string?>
            {
                ["XAI:Enabled"] = "true",
                ["XAI:Endpoint"] = "https://legacy.example/v1",
                ["XAI:ChatEndpoint"] = "http://127.0.0.1:1",
                ["XAI:TimeoutSeconds"] = "1"
            },
            httpClientFactory: httpFactory,
            apiKeyProvider: new TestGrokApiKeyProvider("test-key"));

        var response = await service.AskAsync(new WorkspaceChatRequest(
            "How should we close the gap?",
            "Current workspace context",
            "Water Utility",
            2026));

        Assert.True(response.UsedFallback);
        Assert.False(response.IsFirstConversation);
        Assert.Contains("Legacy fallback answer with live workspace numbers.", response.Answer, StringComparison.Ordinal);

        var request = Assert.Single(httpFactory.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("Bearer test-key", request.Authorization);
        Assert.Contains("Current workspace context", request.Body, StringComparison.Ordinal);
        Assert.Contains("How should we close the gap?", request.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AskAsync_RoutesSemanticKernelChatThroughNamedHttpClient_ForAppRunnerTlsWorkaround()
    {
        // Regression for the App Runner production failure captured in CloudWatch (2026-04-22):
        //   System.Security.Authentication.AuthenticationException:
        //   The remote certificate is invalid because of errors in the certificate chain:
        //   RevocationStatusUnknown, OfflineRevocation
        // App Runner's VPC-connector egress cannot reach public OCSP/CRL responders, so the
        // Semantic Kernel OpenAI connector must use our named IHttpClientFactory client (which
        // registers a SocketsHttpHandler with revocation checks disabled). If SK silently
        // constructs its own HttpClient, the production TLS handshake fails and every chat
        // falls back to the deterministic answer. This test fails closed if the SK path stops
        // requesting the named client.
        var repository = new RecordingConversationRepository();
        SeedPersistedConversation(repository);
        var httpFactory = new TestHttpClientFactory(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"output\":[{\"content\":[{\"text\":\"legacy fallback\"}]}]}", Encoding.UTF8, "application/json")
        });

        var service = CreateService(
            new TestUserContext("user/123", "Alex Morgan", "alex@example.com"),
            repository,
            settings: new Dictionary<string, string?>
            {
                ["XAI:Enabled"] = "true",
                ["XAI:Endpoint"] = "https://legacy.example/v1",
                ["XAI:ChatEndpoint"] = "http://127.0.0.1:1",
                ["XAI:TimeoutSeconds"] = "1"
            },
            httpClientFactory: httpFactory,
            apiKeyProvider: new TestGrokApiKeyProvider("test-key"));

        _ = await service.AskAsync(new WorkspaceChatRequest(
            "How should we close the gap?",
            "Current workspace context",
            "Water Utility",
            2026));

        Assert.Contains(
            WileyWidget.Services.Abstractions.WorkspaceAiKernelFactory.HttpClientName,
            httpFactory.RequestedClientNames);
    }

    [Fact]
    public async Task AskAsync_WhenLegacyReturnsEmpty_UsesDeterministicFallback()
    {
        var repository = new RecordingConversationRepository();
        SeedPersistedConversation(repository);
        var httpFactory = new TestHttpClientFactory(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"output\":[]}", Encoding.UTF8, "application/json")
        });

        var service = CreateService(
            new TestUserContext("user/123", "Alex Morgan", "alex@example.com"),
            repository,
            settings: new Dictionary<string, string?>
            {
                ["XAI:Enabled"] = "true",
                ["XAI:Endpoint"] = "https://legacy.example/v1",
                ["XAI:ChatEndpoint"] = "http://127.0.0.1:1",
                ["XAI:TimeoutSeconds"] = "1"
            },
            httpClientFactory: httpFactory,
            apiKeyProvider: new TestGrokApiKeyProvider("test-key"));

        var response = await service.AskAsync(new WorkspaceChatRequest(
            "What is the current status?",
            "Current workspace context",
            "Water Utility",
            2026));

        Assert.True(response.UsedFallback);
        Assert.False(response.IsFirstConversation);
        Assert.Contains("fallback mode is active", response.Answer, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Runtime diagnostics", response.Answer, StringComparison.OrdinalIgnoreCase);
        Assert.Single(httpFactory.Requests);
    }

    [Fact]
    public async Task AskAsync_WhenLegacyThrows_UsesDeterministicFallback()
    {
        var repository = new RecordingConversationRepository();
        SeedPersistedConversation(repository);
        var httpFactory = new TestHttpClientFactory(_ => throw new HttpRequestException("legacy xai unavailable"));

        var service = CreateService(
            new TestUserContext("user/123", "Alex Morgan", "alex@example.com"),
            repository,
            settings: new Dictionary<string, string?>
            {
                ["XAI:Enabled"] = "true",
                ["XAI:Endpoint"] = "https://legacy.example/v1",
                ["XAI:ChatEndpoint"] = "http://127.0.0.1:1",
                ["XAI:TimeoutSeconds"] = "1"
            },
            httpClientFactory: httpFactory,
            apiKeyProvider: new TestGrokApiKeyProvider("test-key"));

        var response = await service.AskAsync(new WorkspaceChatRequest(
            "What is the current status?",
            "Current workspace context",
            "Water Utility",
            2026));

        Assert.True(response.UsedFallback);
        Assert.False(response.IsFirstConversation);
        Assert.Contains("fallback mode is active", response.Answer, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Runtime diagnostics", response.Answer, StringComparison.OrdinalIgnoreCase);
        Assert.Single(httpFactory.Requests);
    }

    [Fact]
    public async Task AskAsync_HonorsXaiAliasConfiguration_WhenLegacyFallbackExecutes()
    {
        var repository = new RecordingConversationRepository();
        SeedPersistedConversation(repository);
        var httpFactory = new TestHttpClientFactory(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"output\":[{\"content\":[{\"text\":\"Alias fallback answer.\"}]}]}", Encoding.UTF8, "application/json")
        });

        var service = CreateService(
            new TestUserContext("user/123", "Alex Morgan", "alex@example.com"),
            repository,
            settings: new Dictionary<string, string?>
            {
                ["XAI:Enabled"] = "true",
                ["XaiApiKey"] = "alias-key",
                ["XaiApiEndpoint"] = "https://alias.example/v1",
                ["XAI:ChatEndpoint"] = "http://127.0.0.1:1",
                ["XAI:TimeoutSeconds"] = "1"
            },
            httpClientFactory: httpFactory);

        var response = await service.AskAsync(new WorkspaceChatRequest(
            "How should we close the gap?",
            "Current workspace context",
            "Water Utility",
            2026));

        Assert.True(response.UsedFallback);
        Assert.Contains("Alias fallback answer.", response.Answer, StringComparison.Ordinal);

        var request = Assert.Single(httpFactory.Requests);
        Assert.Equal("Bearer alias-key", request.Authorization);
        Assert.Contains("https://alias.example/v1/responses", request.Url, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AskAsync_PrefersExplicitLegacyEndpointOverAlias_WhenLegacyFallbackExecutes()
    {
        var repository = new RecordingConversationRepository();
        SeedPersistedConversation(repository);
        var httpFactory = new TestHttpClientFactory(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"output\":[{\"content\":[{\"text\":\"Proxy fallback answer.\"}]}]}", Encoding.UTF8, "application/json")
        });

        var service = CreateService(
            new TestUserContext("user/123", "Alex Morgan", "alex@example.com"),
            repository,
            settings: new Dictionary<string, string?>
            {
                ["XAI:Enabled"] = "true",
                ["XaiApiKey"] = "alias-key",
                ["XaiApiEndpoint"] = "https://alias.example/v1",
                ["XAI:Endpoint"] = "https://proxy.example/v1",
                ["XAI:ChatEndpoint"] = "http://127.0.0.1:1",
                ["XAI:TimeoutSeconds"] = "1"
            },
            httpClientFactory: httpFactory);

        var response = await service.AskAsync(new WorkspaceChatRequest(
            "How should we close the gap?",
            "Current workspace context",
            "Water Utility",
            2026));

        Assert.True(response.UsedFallback);
        Assert.Contains("Proxy fallback answer.", response.Answer, StringComparison.Ordinal);

        var request = Assert.Single(httpFactory.Requests);
        Assert.Equal("Bearer alias-key", request.Authorization);
        Assert.Contains("https://proxy.example/v1", request.Url, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AskAsync_WhenFallbackPersistenceFails_ReturnsFallbackResponse()
    {
        var innerRepository = new RecordingConversationRepository();
        SeedPersistedConversation(innerRepository);
        var repository = new ThrowingConversationRepository(innerRepository)
        {
            ThrowOnSaveConversation = true
        };

        var service = CreateService(
            new TestUserContext("user/123", "Alex Morgan", "alex@example.com"),
            repository);

        var response = await service.AskAsync(new WorkspaceChatRequest(
            "What is the current status?",
            "Current workspace context",
            "Water Utility",
            2026));

        Assert.True(response.UsedFallback);
        Assert.False(response.IsFirstConversation);
        Assert.Contains("fallback mode is active", response.Answer, StringComparison.OrdinalIgnoreCase);
        await WaitForConditionAsync(() => repository.SaveConversationAttempts > 0);
        Assert.Empty(innerRepository.SavedConversations);
    }

    [Fact]
    public async Task AskAsync_WhenLegacyProxyTimesOut_RetriesDirectXaiEndpoint()
    {
        var repository = new RecordingConversationRepository();
        SeedPersistedConversation(repository);
        var httpFactory = new TestHttpClientFactory(request =>
        {
            if (request.RequestUri?.Host.Contains("legacy.example", StringComparison.OrdinalIgnoreCase) == true)
            {
                throw new HttpRequestException("502 Bad Gateway upstream request timeout");
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"output\":[{\"content\":[{\"text\":\"Direct retry answer from api.x.ai.\"}]}]}", Encoding.UTF8, "application/json")
            };
        });

        var service = CreateService(
            new TestUserContext("user/123", "Alex Morgan", "alex@example.com"),
            repository,
            settings: new Dictionary<string, string?>
            {
                ["XAI:Enabled"] = "true",
                ["XAI:AllowDirectRetry"] = "true",
                ["XAI:Endpoint"] = "https://legacy.example/v1",
                ["XAI:ChatEndpoint"] = "http://127.0.0.1:1",
                ["XAI:TimeoutSeconds"] = "1"
            },
            httpClientFactory: httpFactory,
            apiKeyProvider: new TestGrokApiKeyProvider("test-key"));

        var response = await service.AskAsync(new WorkspaceChatRequest(
            "How should we close the gap?",
            "Current workspace context",
            "Water Utility",
            2026));

        Assert.True(response.UsedFallback);
        Assert.False(response.IsFirstConversation);
        Assert.Contains("Direct retry answer from api.x.ai.", response.Answer, StringComparison.Ordinal);
        Assert.Equal(2, httpFactory.Requests.Count);
        Assert.Contains("https://legacy.example/v1", httpFactory.Requests[0].Url, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("https://api.x.ai/v1/responses", httpFactory.Requests[1].Url, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AskAsync_WhenLegacyProxyTimesOut_DoesNotRetryDirectXaiEndpoint_UnlessEnabled()
    {
        var repository = new RecordingConversationRepository();
        SeedPersistedConversation(repository);
        var httpFactory = new TestHttpClientFactory(request =>
        {
            if (request.RequestUri?.Host.Contains("legacy.example", StringComparison.OrdinalIgnoreCase) == true)
            {
                throw new HttpRequestException("502 Bad Gateway upstream request timeout");
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"output\":[{\"content\":[{\"text\":\"Direct retry answer from api.x.ai.\"}]}]}", Encoding.UTF8, "application/json")
            };
        });

        var service = CreateService(
            new TestUserContext("user/123", "Alex Morgan", "alex@example.com"),
            repository,
            settings: new Dictionary<string, string?>
            {
                ["XAI:Enabled"] = "true",
                ["XAI:Endpoint"] = "https://legacy.example/v1",
                ["XAI:ChatEndpoint"] = "http://127.0.0.1:1",
                ["XAI:TimeoutSeconds"] = "1"
            },
            httpClientFactory: httpFactory,
            apiKeyProvider: new TestGrokApiKeyProvider("test-key"));

        var response = await service.AskAsync(new WorkspaceChatRequest(
            "How should we close the gap?",
            "Current workspace context",
            "Water Utility",
            2026));

        Assert.True(response.UsedFallback);
        Assert.False(response.IsFirstConversation);
        Assert.DoesNotContain("Direct retry answer from api.x.ai.", response.Answer, StringComparison.Ordinal);
        Assert.Single(httpFactory.Requests);
        Assert.Contains("https://legacy.example/v1", httpFactory.Requests[0].Url, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AskAsync_UsesInjectedKernelProviderDiagnostics_WhenLiveKernelIsUnavailable()
    {
        var repository = new RecordingConversationRepository();
        SeedPersistedConversation(repository);

        var service = CreateService(
            new TestUserContext("user/123", "Alex Morgan", "alex@example.com"),
            repository,
            kernelProvider: new TestWorkspaceAiKernelProvider(
                new WorkspaceAiKernelInitializationResult(
                    Context: null,
                    IsAvailable: false,
                    IsApiKeyVisibleToProcess: true,
                    ApiKeySource: "provider:test",
                    StatusCode: "kernel_initialization_failed",
                    StatusMessage: "Injected provider failure")));

        var response = await service.AskAsync(new WorkspaceChatRequest(
            "What is the current status?",
            "Current workspace context",
            "Water Utility",
            2026));

        Assert.True(response.UsedFallback);
        Assert.False(response.IsFirstConversation);
        Assert.Contains("Semantic Kernel initialization failed", response.Answer, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Injected provider failure", response.Answer, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AskAsync_WhenInjectedKernelProviderReportsTransportFailure_AndDirectRetryEnabled_RetriesDirectSemanticKernelChat()
    {
        var repository = new RecordingConversationRepository();
        SeedPersistedConversation(repository);
        var httpFactory = new TestHttpClientFactory(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{" +
                "\"id\":\"chatcmpl-test\"," +
                "\"object\":\"chat.completion\"," +
                "\"created\":1735689600," +
                "\"model\":\"grok-4.20-0309-reasoning\"," +
                "\"choices\":[{" +
                    "\"index\":0," +
                    "\"message\":{\"role\":\"assistant\",\"content\":\"Direct Semantic Kernel retry answer.\"}," +
                    "\"finish_reason\":\"stop\"" +
                "}]," +
                "\"usage\":{\"prompt_tokens\":12,\"completion_tokens\":6,\"total_tokens\":18}" +
            "}", Encoding.UTF8, "application/json")
        });

        var service = CreateService(
            new TestUserContext("user/123", "Alex Morgan", "alex@example.com"),
            repository,
            settings: new Dictionary<string, string?>
            {
                ["XAI:AllowDirectRetry"] = "true"
            },
            httpClientFactory: httpFactory,
            apiKeyProvider: new TestGrokApiKeyProvider("test-key"),
            kernelProvider: new TestWorkspaceAiKernelProvider(
                new WorkspaceAiKernelInitializationResult(
                    Context: null,
                    IsAvailable: false,
                    IsApiKeyVisibleToProcess: true,
                    ApiKeySource: "provider:test",
                    StatusCode: "transport_proxy_failed",
                    StatusMessage: "Injected provider transport failure")));

        var response = await service.AskAsync(new WorkspaceChatRequest(
            "How should we close the gap?",
            "Current workspace context",
            "Water Utility",
            2026));

        Assert.False(response.UsedFallback);
        Assert.False(response.IsFirstConversation);
        Assert.Contains("Direct Semantic Kernel retry answer.", response.Answer, StringComparison.Ordinal);
        Assert.Single(httpFactory.Requests);
        Assert.Contains("api.x.ai", httpFactory.Requests[0].Url, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(WileyWidget.Services.Abstractions.WorkspaceAiKernelFactory.HttpClientName, httpFactory.RequestedClientNames);
    }

    private static WorkspaceAiAssistantService CreateService(
        TestUserContext userContext,
        IConversationRepository repository,
        IReadOnlyDictionary<string, string?>? settings = null,
        IHttpClientFactory? httpClientFactory = null,
        IGrokApiKeyProvider? apiKeyProvider = null,
        IWorkspaceKnowledgeService? knowledgeService = null,
        IWorkspaceAiKernelProvider? kernelProvider = null)
    {
        var configurationValues = new Dictionary<string, string?>
        {
            ["XAI:Enabled"] = "false",
            ["XAI:Endpoint"] = "https://api.x.ai/v1"
        };

        if (settings is not null)
        {
            foreach (var entry in settings)
            {
                configurationValues[entry.Key] = entry.Value;
            }
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationValues)
            .Build();

        var logger = LoggerFactory.Create(builder => { }).CreateLogger<WorkspaceAiAssistantService>();
        var contextService = new TestWileyWidgetContextService();
        return new WorkspaceAiAssistantService(configuration, logger, userContext, repository, contextService, knowledgeService ?? new TestWorkspaceKnowledgeService(), httpClientFactory, apiKeyProvider, kernelProvider);
    }

    private static void SeedPersistedConversation(RecordingConversationRepository repository)
    {
        repository.StoredConversations["jarvis:user-123:water-utility:2026"] = new ConversationHistory
        {
            ConversationId = "jarvis:user-123:water-utility:2026",
            MessagesJson = JsonSerializer.Serialize(new List<WorkspaceChatMessage>
            {
                new("user", "Earlier question"),
                new("assistant", "Earlier answer")
            }, JsonOptions),
            MessageCount = 2,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private static async Task WaitForConditionAsync(Func<bool> condition, int timeoutMilliseconds = 2000, int pollMilliseconds = 25)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMilliseconds);

        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(pollMilliseconds);
        }

        Assert.True(condition(), "Condition was not satisfied before timeout.");
    }

    private sealed class TestWorkspaceKnowledgeService : IWorkspaceKnowledgeService
    {
        public Task<WorkspaceKnowledgeResult> BuildAsync(string enterpriseName, int fiscalYear, CancellationToken cancellationToken = default)
            => Task.FromResult(CreateResult(enterpriseName, fiscalYear));

        public Task<WorkspaceKnowledgeResult> BuildAsync(WorkspaceKnowledgeInput input, CancellationToken cancellationToken = default)
            => Task.FromResult(CreateResult(input.SelectedEnterprise, input.SelectedFiscalYear));

        private static WorkspaceKnowledgeResult CreateResult(string enterpriseName, int fiscalYear)
        {
            return new WorkspaceKnowledgeResult(
                enterpriseName,
                fiscalYear,
                "Action needed",
                $"{enterpriseName} FY {fiscalYear} is below adjusted break-even and needs corrective action.",
                "Live rate rationale based on ledger, reserve, and variance analytics.",
                31.25m,
                98000m,
                4500m,
                1500m,
                21.78m,
                22.11m,
                9.47m,
                9.14m,
                140625m,
                42625m,
                1.43m,
                120000m,
                95000m,
                "Stable",
                DateTime.UtcNow,
                [new WorkspaceKnowledgeInsight("Adjusted gap", "$9.14", "Current rate remains below the adjusted break-even target.")],
                [new WorkspaceKnowledgeAction("Close the gap", "Increase the rate or lower modeled costs.", "High")],
                [new WorkspaceKnowledgeVariance("Chemicals", 10000m, 12500m, 2500m, 25m)]);
        }
    }

    private sealed class TestWileyWidgetContextService : IWileyWidgetContextService
    {
        public Task<string> BuildCurrentSystemContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult("Test municipal finance context for AI plugin validation.");

        public Task<string> GetEnterpriseContextAsync(int enterpriseId, CancellationToken cancellationToken = default)
            => Task.FromResult($"Test enterprise context for ID {enterpriseId}.");

        public Task<string> GetBudgetContextAsync(DateTime? startDate, DateTime? endDate, CancellationToken cancellationToken = default)
            => Task.FromResult("Test budget context with anonymized summaries.");

        public Task<string> GetOperationalContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult("Test operational context with audit metrics.");
    }

    private sealed class TestWorkspaceAiKernelProvider : IWorkspaceAiKernelProvider
    {
        private static readonly WorkspaceAiServiceConfiguration Configuration = new(
            new WorkspaceAiApiKeyResolution("test-api-key", "test-provider", true, false, false, false, null),
            true,
            "grok-4.20-0309-reasoning",
            false,
            new global::System.Uri("https://proxy.example/prod/v1"),
            new global::System.Uri("https://proxy.example/prod/v1/responses"));

        private readonly WorkspaceAiKernelInitializationResult initializationResult;

        public TestWorkspaceAiKernelProvider(WorkspaceAiKernelInitializationResult initializationResult)
        {
            this.initializationResult = initializationResult;
        }

        public WorkspaceAiServiceConfiguration GetConfiguration()
            => Configuration;

        public WorkspaceAiKernelInitializationResult GetInitializationResult()
            => initializationResult;
    }

    private sealed class TestUserContext : IUserContext
    {
        public TestUserContext(string userId, string displayName, string? email)
        {
            UserId = userId;
            DisplayName = displayName;
            Email = email;
        }

        public string? UserId { get; }
        public string? DisplayName { get; }
        public string? Email { get; }
    }

    private sealed class TestGrokApiKeyProvider : IGrokApiKeyProvider
    {
        public TestGrokApiKeyProvider(string apiKey)
        {
            ApiKey = apiKey;
        }

        public string? MaskedApiKey => "test****key";
        public string? ApiKey { get; }
        public bool IsValidated => false;
        public bool IsFromUserSecrets => false;
        public Task<(bool Success, string Message)> ValidateAsync() => Task.FromResult((true, "ok"));
        public string GetConfigurationSource() => "test-provider";
    }

    private sealed class TestHttpClientFactory : IHttpClientFactory
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> responder;

        public TestHttpClientFactory(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            this.responder = responder;
        }

        public List<RequestSnapshot> Requests { get; } = [];

        public List<string> RequestedClientNames { get; } = [];

        public HttpClient CreateClient(string name)
        {
            RequestedClientNames.Add(name);
            return new HttpClient(new CallbackHttpMessageHandler(request =>
            {
                // Tests configure XAI:ChatEndpoint to the loopback sentinel http://127.0.0.1:1
                // so that the Semantic Kernel primary path is guaranteed to fail, exercising
                // the legacy xAI fallback. Now that the SK connector shares this HttpClient
                // (required for the App Runner TLS revocation workaround), the sentinel must
                // continue to fail deterministically rather than hit the responder which is
                // authored for the legacy /responses schema. This short-circuit preserves the
                // original test intent while still recording the request so coverage holds.
                if (request.RequestUri is not null
                    && string.Equals(request.RequestUri.Host, "127.0.0.1", StringComparison.Ordinal)
                    && request.RequestUri.Port == 1)
                {
                    throw new HttpRequestException("Connection refused (loopback sentinel)");
                }

                var realBody = request.Content is null
                    ? string.Empty
                    : request.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                Requests.Add(new RequestSnapshot(
                    request.Method,
                    request.RequestUri?.ToString() ?? string.Empty,
                    request.Headers.Authorization?.ToString(),
                    realBody));

                return responder(request);
            }));
        }
    }

    private sealed record RequestSnapshot(HttpMethod Method, string Url, string? Authorization, string Body);

    private sealed class CallbackHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> responder;

        public CallbackHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            this.responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(responder(request));
        }
    }

    private sealed class RecordingConversationRepository : IConversationRepository
    {
        public List<ConversationHistory> SavedConversations { get; } = [];
        public List<RecommendationHistory> SavedRecommendations { get; } = [];
        public Dictionary<string, ConversationHistory> StoredConversations { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<RecommendationHistory> StoredRecommendations { get; } = [];
        public List<string> DeletedConversationIds { get; } = [];
        public List<string> DeletedRecommendationConversationIds { get; } = [];

        public Task SaveConversationAsync(object conversation, CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            var history = CloneConversation(Assert.IsType<ConversationHistory>(conversation));
            SavedConversations.Add(history);
            StoredConversations[history.ConversationId] = CloneConversation(history);
            return Task.CompletedTask;
        }

        public Task<object?> GetConversationAsync(string id, CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            return Task.FromResult<object?>(StoredConversations.TryGetValue(id, out var conversation) ? CloneConversation(conversation) : null);
        }

        public Task<List<object>> GetConversationsAsync(int skip, int limit, CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            var conversations = StoredConversations.Values
                .Skip(Math.Max(skip, 0))
                .Take(limit <= 0 ? 50 : limit)
                .Select(conversation => (object)CloneConversation(conversation))
                .ToList();

            return Task.FromResult(conversations);
        }

        public Task DeleteConversationAsync(string conversationId, CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            DeletedConversationIds.Add(conversationId);
            StoredConversations.Remove(conversationId);
            return Task.CompletedTask;
        }

        public Task SaveRecommendationAsync(object recommendation, CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            var entry = Assert.IsType<RecommendationHistory>(recommendation);
            var clone = CloneRecommendation(entry);
            SavedRecommendations.Add(clone);
            StoredRecommendations.Add(CloneRecommendation(clone));
            return Task.CompletedTask;
        }

        public Task<List<object>> GetRecommendationsAsync(string userId, string enterprise, int fiscalYear, int limit, CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;

            var results = StoredRecommendations
                .Where(entry => entry.UserId == userId && entry.Enterprise == enterprise && entry.FiscalYear == fiscalYear)
                .OrderByDescending(entry => entry.CreatedAtUtc)
                .Take(limit <= 0 ? 12 : limit)
                .Select(entry => (object)CloneRecommendation(entry))
                .ToList();

            return Task.FromResult(results);
        }

        public Task DeleteRecommendationsAsync(string conversationId, CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            DeletedRecommendationConversationIds.Add(conversationId);
            StoredRecommendations.RemoveAll(entry => entry.ConversationId == conversationId);
            return Task.CompletedTask;
        }

        private static ConversationHistory CloneConversation(ConversationHistory conversation)
        {
            return new ConversationHistory
            {
                Id = conversation.Id,
                ConversationId = conversation.ConversationId,
                Title = conversation.Title,
                Content = conversation.Content,
                MessagesJson = conversation.MessagesJson,
                MessageCount = conversation.MessageCount,
                CreatedAt = conversation.CreatedAt,
                UpdatedAt = conversation.UpdatedAt
            };
        }

        private static RecommendationHistory CloneRecommendation(RecommendationHistory recommendation)
        {
            return new RecommendationHistory
            {
                RecommendationId = recommendation.RecommendationId,
                ConversationId = recommendation.ConversationId,
                UserId = recommendation.UserId,
                UserDisplayName = recommendation.UserDisplayName,
                Enterprise = recommendation.Enterprise,
                FiscalYear = recommendation.FiscalYear,
                Question = recommendation.Question,
                Recommendation = recommendation.Recommendation,
                UsedFallback = recommendation.UsedFallback,
                CreatedAtUtc = recommendation.CreatedAtUtc
            };
        }
    }

    private sealed class ThrowingConversationRepository : IConversationRepository
    {
        private readonly IConversationRepository inner;

        public ThrowingConversationRepository(IConversationRepository inner)
        {
            this.inner = inner;
        }

        public bool ThrowOnSaveConversation { get; init; }
        public bool ThrowOnSaveRecommendation { get; init; }
        public int SaveConversationAttempts { get; private set; }
        public int SaveRecommendationAttempts { get; private set; }

        public Task SaveConversationAsync(object conversation, CancellationToken cancellationToken = default)
        {
            SaveConversationAttempts++;
            if (ThrowOnSaveConversation)
            {
                throw new OperationCanceledException("Simulated persistence cancellation.", cancellationToken);
            }

            return inner.SaveConversationAsync(conversation, cancellationToken);
        }

        public Task<object?> GetConversationAsync(string id, CancellationToken cancellationToken = default)
            => inner.GetConversationAsync(id, cancellationToken);

        public Task<List<object>> GetConversationsAsync(int skip, int limit, CancellationToken cancellationToken = default)
            => inner.GetConversationsAsync(skip, limit, cancellationToken);

        public Task DeleteConversationAsync(string conversationId, CancellationToken cancellationToken = default)
            => inner.DeleteConversationAsync(conversationId, cancellationToken);

        public Task SaveRecommendationAsync(object recommendation, CancellationToken cancellationToken = default)
        {
            SaveRecommendationAttempts++;
            if (ThrowOnSaveRecommendation)
            {
                throw new OperationCanceledException("Simulated persistence cancellation.", cancellationToken);
            }

            return inner.SaveRecommendationAsync(recommendation, cancellationToken);
        }

        public Task<List<object>> GetRecommendationsAsync(string userId, string enterprise, int fiscalYear, int limit, CancellationToken cancellationToken = default)
            => inner.GetRecommendationsAsync(userId, enterprise, fiscalYear, limit, cancellationToken);

        public Task DeleteRecommendationsAsync(string conversationId, CancellationToken cancellationToken = default)
            => inner.DeleteRecommendationsAsync(conversationId, cancellationToken);
    }
}