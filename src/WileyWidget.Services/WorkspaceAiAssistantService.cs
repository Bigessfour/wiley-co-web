using WileyCoWeb.Contracts;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Services;

public sealed class WorkspaceAiAssistantService
{
	private const string SystemPrompt = "You are Jarvis, a municipal finance workspace assistant. Use the live workspace context and the available kernel tools when helpful. Keep answers concise, practical, and specific. If the user asks about codebase structure, time, anomalies, or system behavior, use the registered Semantic Kernel plugins. Do not invent facts that are not in the context or tool output.";
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
	{
		PropertyNameCaseInsensitive = true
	};

	private readonly ILogger<WorkspaceAiAssistantService> logger;
	private readonly IConfiguration configuration;
	private readonly IGrokApiKeyProvider? apiKeyProvider;
	private readonly IUserContext userContext;
	private readonly IConversationRepository conversationRepository;
	private readonly Lazy<KernelContext?> kernelContext;

	public WorkspaceAiAssistantService(
		IConfiguration configuration,
		ILogger<WorkspaceAiAssistantService> logger,
		IUserContext userContext,
		IConversationRepository conversationRepository,
		IGrokApiKeyProvider? apiKeyProvider = null)
	{
		this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
		this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
		this.userContext = userContext ?? throw new ArgumentNullException(nameof(userContext));
		this.conversationRepository = conversationRepository ?? throw new ArgumentNullException(nameof(conversationRepository));
		this.apiKeyProvider = apiKeyProvider;
		kernelContext = new Lazy<KernelContext?>(InitializeKernelContext);
	}

	public async Task<WorkspaceChatResponse> AskAsync(WorkspaceChatRequest request, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(request);

		var question = string.IsNullOrWhiteSpace(request.Question)
			? "What should I know about the current workspace?"
			: request.Question.Trim();
		var activeUser = ResolveCurrentUser();
		var conversationId = BuildConversationId(activeUser, request);
		var conversationHistory = await LoadConversationHistoryAsync(conversationId, request.ConversationHistory, cancellationToken).ConfigureAwait(false);
		var contextSummary = BuildContextSummary(request);

		var assistant = kernelContext.Value;
		if (assistant is null)
		{
			var fallbackResponse = new WorkspaceChatResponse(
				question,
				BuildFallbackAnswer(question, request, activeUser),
				true,
				contextSummary);
			return ApplyUserMetadata(fallbackResponse, activeUser, conversationId, conversationHistory.Count + 1);
		}

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

				var chatResponse = new WorkspaceChatResponse(question, answer, false, contextSummary);
				return ApplyUserMetadata(chatResponse, activeUser, conversationId, conversationHistory.Count);
			}
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Workspace AI assistant request fell back to deterministic guidance");
		}

		var fallbackAnswer = BuildFallbackAnswer(question, request, activeUser);
		conversationHistory = AppendTurn(conversationHistory, question, fallbackAnswer);
		await SaveConversationHistoryAsync(conversationId, activeUser, request, conversationHistory, cancellationToken).ConfigureAwait(false);

		var fallbackChatResponse = new WorkspaceChatResponse(question, fallbackAnswer, true, contextSummary);
		return ApplyUserMetadata(fallbackChatResponse, activeUser, conversationId, conversationHistory.Count);
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

			var model = configuration["Grok:Model"] ?? configuration["XAI:Model"] ?? "grok-4.1";
			var kernelBuilder = Kernel.CreateBuilder();
			kernelBuilder.AddOpenAIChatCompletion(
				modelId: model,
				apiKey: apiKey,
				endpoint: new Uri("https://api.x.ai/v1"));

			kernelBuilder.Plugins.AddFromType<Plugins.System.TimePlugin>();
			kernelBuilder.Plugins.AddFromType<Plugins.Development.CodebaseInsightPlugin>();
			kernelBuilder.Plugins.AddFromType<Plugins.AnomalyDetectionPlugin>();

			var kernel = kernelBuilder.Build();
			var chatService = kernel.GetRequiredService<IChatCompletionService>();

			logger.LogInformation(
				"Workspace AI assistant initialized with Semantic Kernel (model: {Model}, apiKeySource: {ApiKeySource})",
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
		builder.AppendLine($"Workspace context: {BuildContextSummary(request)}");
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

	private WorkspaceChatResponse ApplyUserMetadata(WorkspaceChatResponse response, ResolvedUserContext user, string conversationId, int messageCount)
	{
		return response with
		{
			UserDisplayName = user.DisplayName,
			ConversationId = conversationId,
			ConversationMessageCount = messageCount
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
