using System.Text.Json;
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

    private static WorkspaceAiAssistantService CreateService(TestUserContext userContext, RecordingConversationRepository repository)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["XAI:Enabled"] = "false",
                ["XAI:Endpoint"] = "https://api.x.ai/v1"
            })
            .Build();

        var logger = LoggerFactory.Create(builder => { }).CreateLogger<WorkspaceAiAssistantService>();
        var contextService = new TestWileyWidgetContextService();
        var knowledgeService = new TestWorkspaceKnowledgeService();
        return new WorkspaceAiAssistantService(configuration, logger, userContext, repository, contextService, knowledgeService);
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
}