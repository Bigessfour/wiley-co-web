using System.Globalization;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using WileyCoWeb.Contracts;
using WileyWidget.Services.Abstractions;
using WileyWidget.Services.Plugins;

namespace WileyWidget.Services;

public sealed class WorkspaceAiAssistantService
{
    private const string SystemPrompt = "You are Jarvis, the centerpiece municipal finance AI for rural utility communities. Excel at natural-language conversation: answer 'why is this a certain way?', 'what do we need to do to address this financial issue?' with auditor-impressing, transparent rationales grounded in real ledger data, QuickBooks imports, break-even models, operational methods (reserve building, infrastructure phasing, efficiency gains), GASB/AWWA rural benchmarks, and 5/10-yr trends. Help city councils with limited financial background feel confident in AI suggestions by explaining fluency concepts simply while showing rigorous methodology. Always tie to workspace context, AIContextStore, and UserContextPlugin. Use new functions explain_financial_issue, suggest_operational_actions, generate_rate_rationale for depth. Keep responses practical, human, non-creepy, and actionable for quality council decisions.";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ILogger<WorkspaceAiAssistantService> logger;
    private readonly IConfiguration configuration;
    private readonly IHttpClientFactory? httpClientFactory;
    private readonly IGrokApiKeyProvider? apiKeyProvider;
    private readonly IUserContext userContext;
    private readonly IConversationRepository conversationRepository;
    private readonly IWileyWidgetContextService contextService;
    private readonly Lazy<KernelContext?> kernelContext;
    private readonly bool legacyXaiEnabled;
    private readonly Uri legacyXaiEndpoint;
    private readonly string legacyXaiModel;
    private readonly double legacyXaiTemperature;
    private readonly int legacyXaiMaxTokens;
    private readonly int legacyXaiTimeoutSeconds;

    public WorkspaceAiAssistantService(
        IConfiguration configuration,
        ILogger<WorkspaceAiAssistantService> logger,
        IUserContext userContext,
        IConversationRepository conversationRepository,
        IWileyWidgetContextService contextService,
        IHttpClientFactory? httpClientFactory = null,
        IGrokApiKeyProvider? apiKeyProvider = null)
    {
        this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.userContext = userContext ?? throw new ArgumentNullException(nameof(userContext));
        this.conversationRepository = conversationRepository ?? throw new ArgumentNullException(nameof(conversationRepository));
        this.contextService = contextService ?? throw new ArgumentNullException(nameof(contextService));
        this.httpClientFactory = httpClientFactory;
        this.apiKeyProvider = apiKeyProvider;
        legacyXaiEnabled = configuration.GetValue<bool>("XAI:Enabled", true);
        legacyXaiEndpoint = NormalizeResponsesEndpoint(configuration["XAI:Endpoint"]);
        // Per xAI docs (docs.x.ai/developers/models): use 'grok-4' alias for Grok 4.20 (flagship with 2M context, function calling, low hallucination). Key via Bearer token from AWS Secrets Manager "Grok" (already configured).
        legacyXaiModel = configuration["XAI:Model"] ?? configuration["Grok:Model"] ?? "grok-4";
        legacyXaiTemperature = ParseDouble(configuration["XAI:Temperature"], 0.3d);
        legacyXaiMaxTokens = ParseInt(configuration["XAI:MaxTokens"], 800);
        legacyXaiTimeoutSeconds = ParseInt(configuration["XAI:TimeoutSeconds"], 15);
        kernelContext = new Lazy<KernelContext?>(InitializeKernelContext);
    }

    public async Task<WorkspaceChatResponse> AskAsync(WorkspaceChatRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        logger.LogInformation("Workspace AI request started for {Enterprise} FY {FiscalYear} (question length {QuestionLength})", request.SelectedEnterprise, request.SelectedFiscalYear, request.Question?.Length ?? 0);

        var question = string.IsNullOrWhiteSpace(request.Question)
            ? "What should I know about the current workspace?"
            : request.Question.Trim();
        var activeUser = ResolveCurrentUser();
        var conversationId = BuildConversationId(activeUser, request);
        var conversationHistory = await LoadConversationHistoryAsync(conversationId, request.ConversationHistory, cancellationToken).ConfigureAwait(false);
        var isFirstConversation = conversationHistory.Count == 0;
        var contextSummary = BuildContextSummary(request);

        if (isFirstConversation)
        {
            var onboardingAnswer = BuildOnboardingAnswer(activeUser, request);
            conversationHistory = AppendTurn(conversationHistory, question, onboardingAnswer);
            await SaveConversationHistoryAsync(conversationId, activeUser, request, conversationHistory, cancellationToken).ConfigureAwait(false);
            await SaveRecommendationAsync(conversationId, activeUser, request, question, onboardingAnswer, usedFallback: true, cancellationToken).ConfigureAwait(false);

            var onboardingResponse = new WorkspaceChatResponse(question, onboardingAnswer, true, contextSummary)
            {
                UserDisplayName = activeUser.DisplayName,
                UserProfileSummary = BuildOnboardingProfileSummary(activeUser),
                ConversationId = conversationId,
                ConversationMessageCount = conversationHistory.Count,
                IsFirstConversation = true,
                CanResetConversation = true
            };

            logger.LogInformation("Workspace AI returned onboarding prompt for first conversation {ConversationId}", conversationId);
            return onboardingResponse;
        }

        var assistant = kernelContext.Value;
        if (assistant is not null)
        {
            try
            {
                var chatHistory = new ChatHistory();
                chatHistory.AddSystemMessage(BuildSystemPrompt(activeUser, request));

                foreach (var message in conversationHistory)
                {
                    if (IsAssistantMessage(message.Role))
                    {
                        chatHistory.AddAssistantMessage(message.Content);
                    }
                    else
                    {
                        chatHistory.AddUserMessage(message.Content);
                    }
                }

                chatHistory.AddUserMessage(BuildUserPrompt(question, request, conversationHistory));

                var executionSettings = new OpenAIPromptExecutionSettings
                {
                    ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
                };

                var response = await assistant.ChatService.GetChatMessageContentAsync(chatHistory, executionSettings, assistant.Kernel, cancellationToken).ConfigureAwait(false);
                var answer = response.Content?.Trim();
                if (!string.IsNullOrWhiteSpace(answer))
                {
                    conversationHistory = AppendTurn(conversationHistory, question, answer);
                    await SaveConversationHistoryAsync(conversationId, activeUser, request, conversationHistory, cancellationToken).ConfigureAwait(false);
                    await SaveRecommendationAsync(conversationId, activeUser, request, question, answer, usedFallback: false, cancellationToken).ConfigureAwait(false);

                    var chatResponse = new WorkspaceChatResponse(question, answer, false, contextSummary);
                    logger.LogInformation("Workspace AI returned tool-backed answer for conversation {ConversationId} with {TurnCount} stored turns", conversationId, conversationHistory.Count);
                    return ApplyUserMetadata(chatResponse, activeUser, conversationId, conversationHistory.Count);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Workspace AI assistant request fell back to legacy xAI responses");
            }
        }

        var legacyAnswer = await TryGetLegacyXaiAnswerAsync(activeUser, request, question, conversationHistory, cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(legacyAnswer))
        {
            conversationHistory = AppendTurn(conversationHistory, question, legacyAnswer);
            await SaveConversationHistoryAsync(conversationId, activeUser, request, conversationHistory, cancellationToken).ConfigureAwait(false);
            await SaveRecommendationAsync(conversationId, activeUser, request, question, legacyAnswer, usedFallback: true, cancellationToken).ConfigureAwait(false);

            var legacyResponse = new WorkspaceChatResponse(question, legacyAnswer, true, contextSummary);
            logger.LogInformation("Workspace AI returned legacy xAI fallback answer for conversation {ConversationId} with {TurnCount} stored turns", conversationId, conversationHistory.Count);
            return ApplyUserMetadata(legacyResponse, activeUser, conversationId, conversationHistory.Count);
        }

        var fallbackAnswer = BuildFallbackAnswer(question, request, activeUser);
        conversationHistory = AppendTurn(conversationHistory, question, fallbackAnswer);
        await SaveConversationHistoryAsync(conversationId, activeUser, request, conversationHistory, cancellationToken).ConfigureAwait(false);
        await SaveRecommendationAsync(conversationId, activeUser, request, question, fallbackAnswer, usedFallback: true, cancellationToken).ConfigureAwait(false);

        var fallbackChatResponse = new WorkspaceChatResponse(question, fallbackAnswer, true, contextSummary);
        logger.LogInformation("Workspace AI returned deterministic fallback answer for conversation {ConversationId} with {TurnCount} stored turns", conversationId, conversationHistory.Count);
        return ApplyUserMetadata(fallbackChatResponse, activeUser, conversationId, conversationHistory.Count);
    }

    public async Task ResetConversationAsync(WorkspaceConversationResetRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var activeUser = ResolveCurrentUser();
        var conversationId = BuildConversationId(activeUser, new WorkspaceChatRequest(
            string.Empty,
            request.ContextSummary,
            request.SelectedEnterprise,
            request.SelectedFiscalYear));

        await conversationRepository.DeleteConversationAsync(conversationId, cancellationToken).ConfigureAwait(false);
        await conversationRepository.DeleteRecommendationsAsync(conversationId, cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Deleted Jarvis conversation {ConversationId} for {Enterprise} FY {FiscalYear}", conversationId, request.SelectedEnterprise, request.SelectedFiscalYear);
    }

    public async Task<WorkspaceRecommendationHistoryResponse> GetRecommendationHistoryAsync(WorkspaceRecommendationHistoryRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var activeUser = ResolveCurrentUser();

        var recommendations = await conversationRepository
            .GetRecommendationsAsync(activeUser.UserId, request.SelectedEnterprise, request.SelectedFiscalYear, request.Limit, cancellationToken)
            .ConfigureAwait(false);

        var items = recommendations
            .OfType<RecommendationHistory>()
            .OrderByDescending(entry => entry.CreatedAtUtc)
            .Select(entry => new WorkspaceRecommendationHistoryItem(
                entry.RecommendationId,
                entry.ConversationId,
                entry.UserDisplayName,
                entry.Question,
                entry.Recommendation,
                entry.UsedFallback,
                entry.CreatedAtUtc.ToString("O", CultureInfo.InvariantCulture)))
            .ToList();

        return new WorkspaceRecommendationHistoryResponse(items);
    }

    private KernelContext? InitializeKernelContext()
    {
        try
        {
            var apiKey = apiKeyProvider?.ApiKey
                ?? configuration["XAI:ApiKey"]
                ?? configuration["xAI:ApiKey"]
                ?? configuration["XAI_API_KEY"];
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                logger.LogWarning("Workspace AI assistant is running without an API key; falling back to deterministic responses.");
                return null;
            }

            // Per xAI docs (docs.x.ai/developers/models): 'grok-4' for Grok 4.20 (2M context, function calling, reasoning). Key from AWS Secrets Manager "Grok" secret (configured in Program.cs + amplify.yml).
            var model = configuration["Grok:Model"] ?? configuration["XAI:Model"] ?? "grok-4";
            var kernelBuilder = Kernel.CreateBuilder();
            kernelBuilder.AddOpenAIChatCompletion(
                modelId: model,
                apiKey: apiKey,
                endpoint: new Uri("https://api.x.ai/v1"));

            kernelBuilder.Plugins.AddFromType<Plugins.System.TimePlugin>();
            kernelBuilder.Plugins.AddFromType<Plugins.Development.CodebaseInsightPlugin>();
            kernelBuilder.Plugins.AddFromType<Plugins.AnomalyDetectionPlugin>();

            // Jarvis User Context Plugin - now the AI centerpiece with financial fluency, 'why' explanations,
            // operational recommendations for rural utilities, and auditor-level rate rationales via new functions.
            var userContextPlugin = new UserContextPlugin(userContext, conversationRepository, contextService);
            kernelBuilder.Plugins.AddFromObject(userContextPlugin, "JarvisUserContext");

            var kernel = kernelBuilder.Build();
            var chatService = kernel.GetRequiredService<IChatCompletionService>();

            logger.LogInformation(
                "Workspace AI assistant initialized with Semantic Kernel (model: {Model}, apiKeySource: {ApiKeySource}, plugins: Time+Codebase+Anomaly+JarvisUserContext+FinancialInsights)",
                model,
                apiKeyProvider?.GetConfigurationSource() ?? "configuration");

            return new KernelContext(kernel, chatService);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Workspace AI assistant could not initialize Semantic Kernel");
            return null;
        }
    }

    private async Task<string?> TryGetLegacyXaiAnswerAsync(
        ResolvedUserContext user,
        WorkspaceChatRequest request,
        string question,
        IReadOnlyList<WorkspaceChatMessage> conversationHistory,
        CancellationToken cancellationToken)
    {
        if (!legacyXaiEnabled)
        {
            return null;
        }

        var apiKey = apiKeyProvider?.ApiKey
            ?? configuration["XAI:ApiKey"]
            ?? configuration["xAI:ApiKey"]
            ?? configuration["XAI_API_KEY"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return null;
        }

        var requestBody = new
        {
            input = new[]
            {
                new { role = "system", content = BuildSystemPrompt(user, request) },
                new { role = "user", content = BuildUserPrompt(question, request, conversationHistory) }
            },
            model = legacyXaiModel,
            stream = false,
            temperature = legacyXaiTemperature,
            max_output_tokens = legacyXaiMaxTokens,
            store = true
        };

        var client = httpClientFactory?.CreateClient(nameof(WorkspaceAiAssistantService));
        var ownsClient = false;
        if (client is null)
        {
            client = new HttpClient();
            ownsClient = true;
        }

        client.Timeout = TimeSpan.FromSeconds(legacyXaiTimeoutSeconds);

        try
        {
            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, legacyXaiEndpoint)
            {
                Content = JsonContent.Create(requestBody, options: JsonOptions)
            };
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            var response = await client.SendAsync(requestMessage, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                logger.LogWarning(
                    "Workspace AI legacy xAI fallback returned {StatusCode} for conversation prompt: {Body}",
                    response.StatusCode,
                    responseBody);
                return null;
            }

            var xaiResponse = await response.Content.ReadFromJsonAsync<LegacyXaiResponse>(JsonOptions, cancellationToken).ConfigureAwait(false);
            var answer = xaiResponse?.output?.FirstOrDefault()?.content?.FirstOrDefault()?.text?.Trim();
            if (!string.IsNullOrWhiteSpace(answer))
            {
                return answer;
            }

            logger.LogWarning("Workspace AI legacy xAI fallback returned an empty response.");
            return null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Workspace AI legacy xAI fallback could not produce an answer.");
            return null;
        }
        finally
        {
            if (ownsClient)
            {
                client.Dispose();
            }
        }
    }

    private static Uri NormalizeResponsesEndpoint(string? endpoint)
    {
        var candidate = (endpoint ?? "https://api.x.ai/v1").Trim().TrimEnd('/');
        if (candidate.EndsWith("/responses", StringComparison.OrdinalIgnoreCase))
        {
            return new Uri(candidate, UriKind.Absolute);
        }

        if (candidate.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
        {
            candidate = candidate[..^"/chat/completions".Length];
        }

        return new Uri($"{candidate}/responses", UriKind.Absolute);
    }

    private static int ParseInt(string? value, int fallback)
        => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;

    private static double ParseDouble(string? value, double fallback)
        => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;

    private sealed class LegacyXaiResponse
    {
        public LegacyXaiOutputItem[]? output { get; set; }
        public LegacyXaiError? Error { get; set; }

        public sealed class LegacyXaiOutputItem
        {
            public LegacyXaiContentItem[]? content { get; set; }
        }

        public sealed class LegacyXaiContentItem
        {
            public string? text { get; set; }
        }

        public sealed class LegacyXaiError
        {
            public string? message { get; set; }
            public string? type { get; set; }
        }
    }

    private static string BuildUserPrompt(string question, WorkspaceChatRequest request, IReadOnlyList<WorkspaceChatMessage> conversationHistory)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Workspace context:");
        builder.AppendLine($"Enterprise: {request.SelectedEnterprise}");
        builder.AppendLine($"Fiscal year: {request.SelectedFiscalYear.ToString(CultureInfo.InvariantCulture)}");
        builder.AppendLine($"Context summary: {request.ContextSummary}");

        if (conversationHistory.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Recent conversation:");
            foreach (var message in conversationHistory)
            {
                var roleLabel = IsAssistantMessage(message.Role) ? "Assistant" : "User";
                builder.AppendLine($"{roleLabel}: {message.Content}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("Rules:");
        builder.AppendLine("- Use the registered tools when the question involves codebase details, current time, or anomaly analysis.");
        builder.AppendLine("- Keep the answer short and operational.");
        builder.AppendLine("- If the question is about QuickBooks imports, focus on file format, duplicate prevention, row mapping, and troubleshooting.");
        builder.AppendLine();
        builder.AppendLine($"Question: {question}");
        return builder.ToString();
    }

    private static string BuildContextSummary(WorkspaceChatRequest request)
        => $"{request.SelectedEnterprise} FY {request.SelectedFiscalYear} | {request.ContextSummary}";

    private static string BuildOnboardingAnswer(ResolvedUserContext user, WorkspaceChatRequest request)
        => $"Hi {user.DisplayName}. I’ll keep this thread tied to {request.SelectedEnterprise} FY {request.SelectedFiscalYear}. Tell me your preferred name, your role or department, and what you want Jarvis to help with. I’ll use that to keep the guidance plain-language and relevant.";

    private static string BuildOnboardingProfileSummary(ResolvedUserContext user)
        => $"Onboarding pending for {user.DisplayName}: preferred name, role or department, and Jarvis goals have not been captured yet.";

    private ResolvedUserContext ResolveCurrentUser()
    {
        var userId = string.IsNullOrWhiteSpace(userContext.UserId) ? "anonymous" : userContext.UserId.Trim();
        var displayName = "Guest";
        if (!string.IsNullOrWhiteSpace(userContext.DisplayName))
        {
            displayName = userContext.DisplayName.Trim();
        }
        else if (!string.IsNullOrWhiteSpace(userContext.Email))
        {
            displayName = userContext.Email.Split('@', 2)[0];
        }

        return new ResolvedUserContext(
            userId,
            displayName,
            string.IsNullOrWhiteSpace(userContext.Email) ? null : userContext.Email.Trim(),
            "Prefer concise, operational responses and include the user's name when it improves clarity.");
    }

    private static string BuildSystemPrompt(ResolvedUserContext user, WorkspaceChatRequest request)
    {
        var builder = new StringBuilder(SystemPrompt);
        builder.AppendLine();
        builder.AppendLine($"Resolved user: {user.DisplayName}");
        builder.AppendLine($"User identifier: {user.UserId}");
        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            builder.AppendLine($"User email: {user.Email}");
        }

        builder.AppendLine($"User preference summary: {user.PreferencesSummary}");
        builder.AppendLine($"Workspace focus: {BuildContextSummary(request)}");
        return builder.ToString();
    }

    private async Task<IReadOnlyList<WorkspaceChatMessage>> LoadConversationHistoryAsync(string conversationId, IReadOnlyList<WorkspaceChatMessage>? sessionHistory, CancellationToken cancellationToken)
    {
        var persisted = await ReadPersistedConversationAsync(conversationId, cancellationToken).ConfigureAwait(false);
        if (persisted.Count > 0)
        {
            return persisted;
        }

        return NormalizeConversationHistory(sessionHistory);
    }

    private static IReadOnlyList<WorkspaceChatMessage> AppendTurn(IReadOnlyList<WorkspaceChatMessage> history, string userMessage, string assistantMessage)
    {
        var updated = history.ToList();
        updated.Add(new WorkspaceChatMessage("user", userMessage));
        updated.Add(new WorkspaceChatMessage("assistant", assistantMessage));
        return updated.TakeLast(12).ToArray();
    }

    private async Task SaveConversationHistoryAsync(string conversationId, ResolvedUserContext user, WorkspaceChatRequest request, IReadOnlyList<WorkspaceChatMessage> conversationHistory, CancellationToken cancellationToken)
    {
        var conversation = new ConversationHistory
        {
            ConversationId = conversationId,
            Title = $"{user.DisplayName} | {request.SelectedEnterprise} FY {request.SelectedFiscalYear}",
            Content = $"Jarvis conversation for {user.DisplayName} on {request.SelectedEnterprise} FY {request.SelectedFiscalYear}. {user.PreferencesSummary}",
            MessagesJson = JsonSerializer.Serialize(conversationHistory, JsonOptions),
            MessageCount = conversationHistory.Count,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await conversationRepository.SaveConversationAsync(conversation, cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Saved Jarvis conversation {ConversationId} for {Enterprise} FY {FiscalYear} with {MessageCount} messages", conversationId, request.SelectedEnterprise, request.SelectedFiscalYear, conversationHistory.Count);
    }

    private async Task SaveRecommendationAsync(string conversationId, ResolvedUserContext user, WorkspaceChatRequest request, string question, string answer, bool usedFallback, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(answer))
        {
            return;
        }

        var recommendation = new RecommendationHistory
        {
            RecommendationId = $"reco:{Guid.NewGuid():N}",
            ConversationId = conversationId,
            UserId = user.UserId,
            UserDisplayName = user.DisplayName,
            Enterprise = request.SelectedEnterprise,
            FiscalYear = request.SelectedFiscalYear,
            Question = question,
            Recommendation = answer,
            UsedFallback = usedFallback,
            CreatedAtUtc = DateTime.UtcNow
        };

        await conversationRepository.SaveRecommendationAsync(recommendation, cancellationToken).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<WorkspaceChatMessage>> ReadPersistedConversationAsync(string conversationId, CancellationToken cancellationToken)
    {
        var storedConversation = await conversationRepository.GetConversationAsync(conversationId, cancellationToken).ConfigureAwait(false);
        if (storedConversation is not ConversationHistory conversationHistory || string.IsNullOrWhiteSpace(conversationHistory.MessagesJson))
        {
            return Array.Empty<WorkspaceChatMessage>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<WorkspaceChatMessage>>(conversationHistory.MessagesJson, JsonOptions)
                ?.Where(message => !string.IsNullOrWhiteSpace(message.Content))
                .Select(message => new WorkspaceChatMessage(message.Role.Trim(), message.Content.Trim()))
                .TakeLast(12)
                .ToArray()
                ?? Array.Empty<WorkspaceChatMessage>();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Unable to read persisted Jarvis conversation {ConversationId}", conversationId);
            return Array.Empty<WorkspaceChatMessage>();
        }
    }

    private WorkspaceChatResponse ApplyUserMetadata(WorkspaceChatResponse response, ResolvedUserContext user, string conversationId, int messageCount, string? userProfileSummary = null)
    {
        return response with
        {
            UserDisplayName = user.DisplayName,
            UserProfileSummary = userProfileSummary ?? user.PreferencesSummary,
            ConversationId = conversationId,
            ConversationMessageCount = messageCount,
            IsFirstConversation = messageCount <= 2,
            CanResetConversation = true
        };
    }

    private static string BuildConversationId(ResolvedUserContext user, WorkspaceChatRequest request)
        => $"jarvis:{SanitizeKey(user.UserId)}:{SanitizeKey(request.SelectedEnterprise)}:{request.SelectedFiscalYear.ToString(CultureInfo.InvariantCulture)}";

    private static string SanitizeKey(string value)
    {
        var normalized = new string(value.ToLowerInvariant().Select(character => char.IsLetterOrDigit(character) ? character : '-').ToArray());
        while (normalized.Contains("--", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("--", "-", StringComparison.Ordinal);
        }

        return normalized.Trim('-');
    }

    private static IReadOnlyList<WorkspaceChatMessage> NormalizeConversationHistory(IReadOnlyList<WorkspaceChatMessage>? conversationHistory)
    {
        if (conversationHistory is null || conversationHistory.Count == 0)
        {
            return Array.Empty<WorkspaceChatMessage>();
        }

        return conversationHistory
            .Where(message => !string.IsNullOrWhiteSpace(message.Content))
            .Select(message => new WorkspaceChatMessage(message.Role.Trim(), message.Content.Trim()))
            .TakeLast(12)
            .ToArray();
    }

    private static bool IsAssistantMessage(string role)
        => string.Equals(role, "assistant", StringComparison.OrdinalIgnoreCase) || string.Equals(role, "jarvis", StringComparison.OrdinalIgnoreCase);

    private static string BuildFallbackAnswer(string question, WorkspaceChatRequest request, ResolvedUserContext user)
    {
        var lowered = question.ToLowerInvariant();

        if (lowered.Contains("time"))
        {
            return $"{user.DisplayName}, current workspace context: {BuildContextSummary(request)}. The live kernel is running in fallback mode, so tool-based answers are limited until an API key is configured.";
        }

        if (lowered.Contains("code") || lowered.Contains("plugin") || lowered.Contains("kernel"))
        {
            return $"{user.DisplayName}, the codebase insight plugin is available in the services archive and can be used once the API key is configured. Ask for source files, AI architecture, or plugin inventory details.";
        }

        if (lowered.Contains("anomal") || lowered.Contains("variance"))
        {
            return $"{user.DisplayName}, the anomaly detection plugin is available for dataset and variance analysis. Once xAI is configured, the assistant can call it through Semantic Kernel for live results.";
        }

        return $"{user.DisplayName}, Jarvis fallback mode is active for {BuildContextSummary(request)}. Configure the xAI key to enable full Semantic Kernel chat with tool invocation.";
    }

    private sealed record ResolvedUserContext(string UserId, string DisplayName, string? Email, string PreferencesSummary);

    private sealed record KernelContext(Kernel Kernel, IChatCompletionService ChatService);
}
