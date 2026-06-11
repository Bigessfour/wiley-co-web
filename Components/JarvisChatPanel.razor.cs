using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using System.Globalization;
using Syncfusion.Blazor.InteractiveChat;
using WileyCoWeb.Contracts;
using WileyCoWeb.Services;
using WileyCoWeb.State;

namespace WileyCoWeb.Components;

public partial class JarvisChatPanel : ComponentBase, IDisposable
{
    private SfAIAssistView? JarvisAssistView { get; set; }

    private readonly List<WorkspaceChatMessage> chatTranscript = [];
    private readonly List<AssistViewPrompt> chatPrompts = [];
    private readonly List<WorkspaceRecommendationHistoryItem> recommendationHistory = [];
    private readonly List<string> promptSuggestions = [
        "What changed in the current workspace?",
        "How far is the current rate from break-even?",
        "What should I review before publishing?",
        "Summarize the current scenario pressure."
    ];

    private Action? workspaceChangedHandler;
    private string? lastKnowledgeFingerprint;
    private string? lastRecommendationHistoryScope;
    private WorkspaceKnowledgeResponse? workspaceKnowledge;

    [Inject]
    protected WorkspaceState WorkspaceState { get; set; } = default!;

    [Inject]
    protected WorkspaceAiApiService AiApi { get; set; } = default!;

    [Inject]
    protected IServiceProvider ServiceProvider { get; set; } = default!;

    [Inject]
    protected ILogger<JarvisChatPanel> Logger { get; set; } = default!;

    [Inject]
    protected IJSRuntime JSRuntime { get; set; } = default!;

    /// <summary>When true (Decision Support panel), renders the Jarvis chat block above insights and recommendations.</summary>
    [Parameter]
    public bool ChatFirstLayout { get; set; }

    /// <summary>Stable DOM id for SfAIAssistView; must be unique when multiple JarvisChatPanel instances render (dock vs Decision Support).</summary>
    [Parameter]
    public string AssistViewId { get; set; } = "jarvis-chat-ui";

    private bool _scrolledChatIntoView;

    protected List<AssistViewPrompt> ChatPrompts => chatPrompts;
    public List<string> PromptSuggestions => promptSuggestions;
    protected string StatusText { get; set; } = "Live guidance pending";
    protected string PrimaryBrief { get; set; } = "Loading live workspace guidance.";
    protected IReadOnlyList<InsightCard> Insights { get; set; } = [];
    protected IReadOnlyList<RecommendationItem> RecommendedActions { get; set; } = [];
    public IReadOnlyList<WorkspaceRecommendationHistoryItem> RecommendationHistory => recommendationHistory;
    protected IReadOnlyList<WorkspaceChatMessage> ChatTranscript => chatTranscript;
    protected string ChatQuestion { get; set; } = "What should I know about the current workspace?";
    protected string ChatAnswer { get; set; } = "Ask Jarvis a question about the workspace, codebase, or AI tools.";
    protected string ChatContextSummary => BuildChatContextSummary();
    protected string ChatRuntimeStatusTitle { get; set; } = "Awaiting Jarvis response";
    protected string ChatRuntimeStatusDetail { get; set; } = "Submit a prompt to verify whether the server is returning live AI guidance or fallback mode.";
    protected string KnowledgeStatus { get; set; } = "Waiting for live workspace guidance.";
    protected string RecommendationHistoryStatus { get; set; } = "Recommendation history will appear here after Jarvis saves a recommendation.";
    protected string CurrentUserLabel { get; set; } = "Guest";
    protected string CurrentConversationLabel { get; set; } = "Local session";
    protected string CurrentProfileSummary { get; set; } = "Jarvis will ask a few setup questions on first contact.";
    protected bool IsChatBusy { get; set; }
    protected bool IsChatFallbackActive { get; set; }
    protected bool IsKnowledgeBusy { get; set; }
    protected bool CanAskChat => !IsChatBusy && !string.IsNullOrWhiteSpace(ChatQuestion);
    public bool IsSecureJarvisEnabled => !string.Equals(Environment.GetEnvironmentVariable("UI__UseSecureJarvis"), "false", StringComparison.OrdinalIgnoreCase);

    // --- Local dev xAI key setup (production-safe: endpoint + storage only exist in Development) ---
    // Kept as the canonical method: key is added/rotated exclusively via the Jarvis panel prompt.
    // Never exchanged in chat or committed. Persisted only to gitignored local file.
    protected bool ShowXaiKeySetupPrompt { get; set; }
    protected bool ShowXaiKeyInputForm { get; set; }
    protected string XaiKeyInput { get; set; } = string.Empty;
    protected bool IsSettingXaiKey { get; set; }
    protected string XaiKeySetupMessage { get; set; } = string.Empty;
    protected string XaiKeySetupStatus { get; set; } = string.Empty;

    // Always offer a dev-only key manager (local only) for easy access/rotation
    // even after the initial prompt. This completes the "add xai key via jarvis chat window" flow.
    // The server endpoint is guarded to Development only.
    protected bool ShowDevKeyManager => DevKeyManagerExpanded;

    // Simple flag to toggle the manager UI (collapsed by default after first use)
    protected bool DevKeyManagerExpanded { get; set; } = false;

    protected bool CanSubmitXaiKey => !IsSettingXaiKey && !string.IsNullOrWhiteSpace(XaiKeyInput);

    protected override void OnInitialized()
    {
        ApplyFallbackKnowledge("Waiting for live workspace guidance.");
        workspaceChangedHandler = () => _ = InvokeAsync(HandleWorkspaceChangedAsync);
        WorkspaceState.Changed += workspaceChangedHandler;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        await LoadRecommendationHistoryAsync(force: true).ConfigureAwait(false); // Persists via EfConversationRepository + UserContextPlugin (history now auditable)
        await RefreshKnowledgeAsync(force: true).ConfigureAwait(false);
        await CheckForMissingXaiKeyAtStartupAsync().ConfigureAwait(false);

        if (ChatFirstLayout && !_scrolledChatIntoView)
        {
            _scrolledChatIntoView = true;
            await JSRuntime.InvokeVoidAsync("wileyWorkspace.scrollIntoViewById", AssistViewId);
        }

        await InvokeAsync(StateHasChanged);
    }

    /// <summary>
    /// "Startup" hook: checks AI health. If fallbacks are active (typical when no xAI key for local dev),
    /// we surface a friendly prompt so the developer can add/rotate the key with Yes/No buttons.
    /// This only makes sense / is useful in Development.
    /// </summary>
    private async Task CheckForMissingXaiKeyAtStartupAsync()
    {
        try
        {
            var health = await AiApi.GetHealthAsync().ConfigureAwait(false);
            if (health is null)
            {
                Logger.LogWarning("Jarvis startup health check failed: /api/ai/health returned null.");
                ChatRuntimeStatusTitle = "AI health unavailable";
                ChatRuntimeStatusDetail = "Could not reach /api/ai/health. Confirm the API is running on http://localhost:5231 and the client base address points to it.";
                return;
            }

            Logger.LogInformation(
                "Jarvis startup health: LatestUsedFallback={LatestUsedFallback} SemanticKernelAvailable={SemanticKernelAvailable} LatestAnswerSource={LatestAnswerSource}",
                health.LatestUsedFallback,
                health.SemanticKernelAvailable,
                health.LatestAnswerSource);

            if (health.LatestUsedFallback)
            {
                ShowXaiKeySetupPrompt = true;
                XaiKeySetupStatus = "Jarvis is using safe fallback responses (no live Grok turn yet, or xAI key missing).";
                UpdateChatRuntimeStatus(true, health.LatestAnswerSource);
                ChatRuntimeStatusDetail = "Set XAI_API_KEY (or use Dev: xAI Key below), restart the API, then send a prompt to verify live Grok.";
                return;
            }

            UpdateChatRuntimeStatus(false, health.LatestAnswerSource);
            if (!health.SemanticKernelAvailable)
            {
                ChatRuntimeStatusTitle = "Semantic Kernel unavailable";
                ChatRuntimeStatusDetail = "The API health check reports Semantic Kernel is not initialized. Set XAI_API_KEY before starting WileyCoWeb.Api, then restart the API.";
                ShowXaiKeySetupPrompt = true;
                XaiKeySetupStatus = "Live Grok requires a valid xAI key at API startup.";
            }
            else if (string.Equals(health.Status, "healthy", StringComparison.OrdinalIgnoreCase))
            {
                ChatRuntimeStatusDetail = "Semantic Kernel is available. Send a prompt to confirm live Grok; expand Dev: xAI Key if you still need to configure the key locally.";
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Jarvis startup health check failed.");
            ChatRuntimeStatusTitle = "AI health check failed";
            ChatRuntimeStatusDetail = "Could not load /api/ai/health. Start the API (dotnet run --project WileyCoWeb.Api) and reload the workspace.";
        }
    }

    protected void OnXaiKeyPromptYes()
    {
        XaiKeySetupMessage = string.Empty;
        ShowXaiKeyInputForm = true;   // reveal the password input + save button
    }

    protected void OnXaiKeyPromptNo()
    {
        ShowXaiKeySetupPrompt = false;
        ShowXaiKeyInputForm = false;
        XaiKeySetupMessage = "You can always add the key later from the Jarvis rail or by setting XAI_API_KEY / user secrets before starting the API.";
        InvokeAsync(StateHasChanged);
    }

    protected async Task SubmitXaiKeyAsync()
    {
        if (string.IsNullOrWhiteSpace(XaiKeyInput))
        {
            XaiKeySetupMessage = "Please enter a key (starts with sk- or similar).";
            return;
        }

        IsSettingXaiKey = true;
        XaiKeySetupMessage = string.Empty;
        await InvokeAsync(StateHasChanged);

        try
        {
            bool ok = await AiApi.SetDevXaiApiKeyAsync(XaiKeyInput).ConfigureAwait(false);
            if (ok)
            {
                XaiKeySetupMessage = "Key accepted and saved to your local development settings (gitignored). " +
                                     "For full effect in this Jarvis session you may need to restart the API process. " +
                                     "Refresh or try a new prompt.";
                ShowXaiKeySetupPrompt = false; // hide after success; user can re-trigger if needed
                // Clear sensitive input
                XaiKeyInput = string.Empty;
            }
            else
            {
                XaiKeySetupMessage = "Could not save the key (the dev endpoint may not be available in this environment). " +
                                     "Use dotnet user-secrets set \"XAI_API_KEY\" \"sk-...\" --project WileyCoWeb.Api/WileyCoWeb.Api.csproj or export the env var instead.";
            }
        }
        catch (Exception ex)
        {
            XaiKeySetupMessage = $"Error saving key: {ex.Message}. See README for manual alternatives (user secrets / launchctl / local json).";
        }
        finally
        {
            IsSettingXaiKey = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    protected Task RefreshKnowledgeFromButtonAsync()
    {
        return RefreshKnowledgeAsync(force: true);
    }

    protected async Task OnPromptRequestedAsync(AssistViewPromptRequestedEventArgs args)
    {
        await SubmitPromptAsync(string.IsNullOrWhiteSpace(args?.Prompt) ? ChatQuestion : args.Prompt.Trim(), args);
    }

    public Task AskChatAsync()
    {
        return SubmitPromptAsync(ChatQuestion);
    }

    protected async Task SubmitFooterPromptAsync()
    {
        if (!CanAskChat)
        {
            return;
        }

        if (JarvisAssistView is null)
        {
            await SubmitPromptAsync(ChatQuestion);
            return;
        }

        var prompt = ChatQuestion.Trim();
        await JarvisAssistView.ExecutePromptAsync(prompt).ConfigureAwait(false);
        ChatQuestion = string.Empty;
        await InvokeAsync(StateHasChanged);
    }

    public async Task ResetChatAsync()
    {
        await ExecuteChatOperationAsync(BeginChatReset, ResetChatCoreAsync, HandleChatResetFailure, EndChatReset);
    }

    public Task ClearChatAsync()
    {
        ChatQuestion = "What should I know about the current workspace?";
        ChatAnswer = "Ask Jarvis a question about the workspace, codebase, or AI tools.";
        chatTranscript.Clear();
        chatPrompts.Clear();
        recommendationHistory.Clear();
        return InvokeAsync(StateHasChanged);
    }

    private async Task SubmitPromptAsync(string question, AssistViewPromptRequestedEventArgs? args = null)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            ChatAnswer = "Enter a question before asking Jarvis.";
            if (args is not null)
            {
                args.Cancel = true;
            }

            await InvokeAsync(StateHasChanged);
            return;
        }

        await ExecuteChatOperationAsync(
            () => BeginChatSubmission(question),
            () => SubmitPromptCoreAsync(question, args),
            ex => HandleChatSubmissionFailure(ex, args),
            EndChatSubmission);
    }

    private string GetStatusText()
    {
        var rateGap = WorkspaceState.AdjustedRecommendedRate - WorkspaceState.CurrentRate;
        if (rateGap > 0)
        {
            return "Action needed";
        }

        return WorkspaceState.ScenarioItems.Count > 0 ? "Scenario monitored" : "On target";
    }

    private string BuildPrimaryBrief()
    {
        var adjustedBreakEven = WorkspaceState.AdjustedRecommendedRate;
        var rateGap = adjustedBreakEven - WorkspaceState.CurrentRate;
        var gapDisplay = Math.Abs(rateGap).ToString("C2");

        if (rateGap > 0)
        {
            return $"{WorkspaceState.ContextSummary} is currently below the adjusted break-even target by {gapDisplay}. Raise the working rate or reduce scenario costs before publishing a recommendation.";
        }

        if (rateGap < 0)
        {
            return $"{WorkspaceState.ContextSummary} is above the adjusted break-even target by {gapDisplay}. The current rate covers the modeled cost profile and can absorb active scenario items.";
        }

        return $"{WorkspaceState.ContextSummary} is exactly aligned to the adjusted break-even target. Snapshot the current scenario and review customer sensitivity before final approval.";
    }

    private string BuildChatContextSummary()
    {
        if (workspaceKnowledge is not null)
        {
            return $"{WorkspaceState.ContextSummary}; operational status {workspaceKnowledge.OperationalStatus}; adjusted rate gap {workspaceKnowledge.AdjustedRateGap:C2}; net position {workspaceKnowledge.NetPosition:C0}; reserve risk {workspaceKnowledge.ReserveRiskAssessment}.";
        }

        var rateGap = WorkspaceState.AdjustedRecommendedRate - WorkspaceState.CurrentRate;
        return $"{WorkspaceState.ContextSummary}; rate gap {rateGap:C2}; scenario costs {WorkspaceState.ScenarioCostTotal:C0}; customers {WorkspaceState.FilteredCustomerCount}.";
    }

    private async Task HandleWorkspaceChangedAsync()
    {
        await RefreshKnowledgeAsync().ConfigureAwait(false);
        await LoadRecommendationHistoryAsync().ConfigureAwait(false);
        await InvokeAsync(StateHasChanged);
    }

    private async Task RefreshKnowledgeAsync(bool force = false)
    {
        var fingerprint = BuildKnowledgeFingerprint();
        if (TryHandleKnowledgeRefreshPreconditions(force, fingerprint, out var knowledgeApi))
        {
            return;
        }

        await ExecuteChatOperationAsync(
            BeginKnowledgeRefresh,
            () => RefreshKnowledgeCoreAsync(knowledgeApi!, fingerprint),
            ex => HandleKnowledgeRefreshFailure(ex, fingerprint),
            EndKnowledgeRefresh);
    }

    private string BuildKnowledgeFingerprint()
    {
        return string.Join("|", BuildKnowledgeFingerprintParts(WorkspaceState.ToBootstrapData()));
    }

    private void ApplyKnowledge(WorkspaceKnowledgeResponse knowledge)
    {
        StatusText = knowledge.OperationalStatus;
        PrimaryBrief = knowledge.ExecutiveSummary;
        ApplyKnowledgeInsights(knowledge);
        ApplyKnowledgeRecommendations(knowledge);
        UpdateKnowledgeStatus(knowledge.GeneratedAtUtc);
    }

    private void ApplyFallbackKnowledge(string status)
    {
        StatusText = GetStatusText();
        PrimaryBrief = BuildPrimaryBrief();
        Insights = BuildInsights();
        RecommendedActions = BuildRecommendations();
        KnowledgeStatus = status;
    }

    private static string FormatGeneratedAt(string generatedAtUtc)
    {
        return DateTimeOffset.TryParse(generatedAtUtc, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)
            : "just now";
    }

    protected static bool IsAssistantMessage(string role)
    {
        return string.Equals(role, "assistant", StringComparison.OrdinalIgnoreCase) || string.Equals(role, "jarvis", StringComparison.OrdinalIgnoreCase);
    }

    private List<WorkspaceChatMessage> BuildConversationHistory()
    {
        if (chatTranscript.Count == 0)
        {
            return [];
        }

        return [.. chatTranscript.TakeLast(12)];
    }

    private async Task LoadRecommendationHistoryAsync(bool force = false)
    {
        if (TryHandleRecommendationHistoryPreconditions(force, out var recommendationHistoryScope))
        {
            return;
        }

        try
        {
            var history = await AiApi.GetRecommendationHistoryAsync(new WorkspaceRecommendationHistoryRequest(
                WorkspaceState.SelectedEnterprise,
                WorkspaceState.SelectedFiscalYear,
                8)).ConfigureAwait(false);

            recommendationHistory.Clear();
            recommendationHistory.AddRange(history.Items);

            UpdateChatRuntimeStatusFromHistory();

            RecommendationHistoryStatus = recommendationHistory.Count == 0
                ? "No saved recommendations yet for this workspace scope."
                : $"Loaded {recommendationHistory.Count} saved recommendation{(recommendationHistory.Count == 1 ? string.Empty : "s")} for this workspace scope.";
            lastRecommendationHistoryScope = recommendationHistoryScope;
        }
        catch (Exception ex)
        {
            RecommendationHistoryStatus = $"Recommendation history is unavailable right now: {ex.Message}";
        }
    }

    private bool TryHandleRecommendationHistoryPreconditions(bool force, out string recommendationHistoryScope)
    {
        recommendationHistoryScope = BuildRecommendationHistoryScope();

        if (string.IsNullOrWhiteSpace(recommendationHistoryScope))
        {
            recommendationHistory.Clear();
            RecommendationHistoryStatus = "Recommendation history will load after an enterprise and fiscal year are available.";
            lastRecommendationHistoryScope = null;
            return true;
        }

        if (!force && string.Equals(lastRecommendationHistoryScope, recommendationHistoryScope, StringComparison.Ordinal))
        {
            return true;
        }

        return false;
    }

    private string BuildRecommendationHistoryScope()
    {
        if (string.IsNullOrWhiteSpace(WorkspaceState.SelectedEnterprise) || WorkspaceState.SelectedFiscalYear <= 0)
        {
            return string.Empty;
        }

        return $"{WorkspaceState.SelectedEnterprise.Trim()}|{WorkspaceState.SelectedFiscalYear.ToString(CultureInfo.InvariantCulture)}";
    }

    private void UpdateChatRuntimeStatus(bool usedFallback, string? answerSource = null)
    {
        IsChatFallbackActive = usedFallback;

        if (usedFallback)
        {
            ChatRuntimeStatusTitle = string.IsNullOrWhiteSpace(answerSource)
                ? "AI runtime unavailable"
                : $"Deterministic fallback ({answerSource})";
            ChatRuntimeStatusDetail = "Jarvis is reaching the server, but the server is returning fallback guidance instead of a live xAI or Semantic Kernel response. Check xAI key and endpoint configuration.";
            return;
        }

        ChatRuntimeStatusTitle = string.IsNullOrWhiteSpace(answerSource)
            ? "Live AI available"
            : $"Live AI ({answerSource})";
        ChatRuntimeStatusDetail = "Jarvis returned a live AI response for this workspace scope.";
    }

    private void UpdateChatRuntimeStatusFromHistory()
    {
        if (recommendationHistory.Count == 0)
        {
            return;
        }

        UpdateChatRuntimeStatus(recommendationHistory[0].UsedFallback);
    }

    private void AppendUserMessage(string message)
    {
        chatTranscript.Add(new WorkspaceChatMessage("user", message));
    }

    private void AppendAssistantMessage(string message)
    {
        chatTranscript.Add(new WorkspaceChatMessage("assistant", message));
    }

    private IReadOnlyList<InsightCard> BuildInsights()
    {
        return [
            CreateRateGapInsight(),
            CreateScenarioPressureInsight(),
            CreateCustomerScopeInsight(),
            CreateProjectionDriftInsight()
        ];
    }

    private List<RecommendationItem> BuildRecommendations()
    {
        var recommendations = new List<RecommendationItem>();
        AddRateGapRecommendation(recommendations);
        AddScenarioRecommendation(recommendations);
        AddCustomerMixRecommendation(recommendations);
        return recommendations;
    }

    private static IEnumerable<string> BuildKnowledgeFingerprintParts(WorkspaceBootstrapData snapshot)
    {
        return [
            snapshot.SelectedEnterprise ?? string.Empty,
            GetFingerprintSelectedFiscalYear(snapshot),
            GetFingerprintCurrentRate(snapshot),
            GetFingerprintTotalCosts(snapshot),
            GetFingerprintProjectedVolume(snapshot),
            GetFingerprintScenarioCostTotal(snapshot),
            GetFingerprintScenarioCount(snapshot)
        ];
    }

    private void ApplyKnowledgeInsights(WorkspaceKnowledgeResponse knowledge)
    {
        Insights = knowledge.Insights
            .Select(item => new InsightCard(item.Label, item.Value, item.Description))
            .ToArray();

        if (Insights.Count == 0)
        {
            Insights = BuildInsights();
        }
    }

    private void ApplyKnowledgeRecommendations(WorkspaceKnowledgeResponse knowledge)
    {
        RecommendedActions = knowledge.RecommendedActions
            .Select(item => new RecommendationItem(
                string.IsNullOrWhiteSpace(item.Priority) ? item.Title : $"{item.Title} ({item.Priority})",
                item.Description))
            .ToArray();

        if (RecommendedActions.Count == 0)
        {
            RecommendedActions = BuildRecommendations();
        }
    }

    private void UpdateKnowledgeStatus(string generatedAtUtc)
    {
        KnowledgeStatus = $"Live guidance refreshed {FormatGeneratedAt(generatedAtUtc)}.";
    }

    private InsightCard CreateRateGapInsight()
    {
        var adjustedBreakEven = WorkspaceState.AdjustedRecommendedRate;
        var rateGap = adjustedBreakEven - WorkspaceState.CurrentRate;

        return new InsightCard(
            "Rate gap",
            rateGap.ToString("C2"),
            rateGap > 0 ? "Positive values indicate the rate is below the adjusted break-even target." : "Negative values indicate coverage above the adjusted break-even target.");
    }

    private InsightCard CreateScenarioPressureInsight()
    {
        return new InsightCard(
            "Scenario pressure",
            WorkspaceState.ScenarioCostTotal.ToString("C0"),
            "Combined impact of all active scenario items on the current workspace.");
    }

    private InsightCard CreateCustomerScopeInsight()
    {
        return new InsightCard(
            "Customer scope",
            WorkspaceState.FilteredCustomerCount.ToString(),
            "Filtered customer records currently contributing to the viewer and service mix review.");
    }

    private InsightCard CreateProjectionDriftInsight()
    {
        return new(
        "Projection drift",
        GetProjectionDrift().ToString("C2"),
        "Difference between the first and last projected rates in the trend series.");
    }

    private void AddRateGapRecommendation(List<RecommendationItem> recommendations)
    {
        var rateGap = WorkspaceState.AdjustedRecommendedRate - WorkspaceState.CurrentRate;

        if (rateGap > 0)
        {
            recommendations.Add(new RecommendationItem(
                "Close the modeled rate gap",
                $"Increase the working rate by {rateGap:C2} or offset the same amount through cost reductions before finalizing the scenario."));
            return;
        }

        recommendations.Add(new RecommendationItem(
            "Preserve current coverage",
            "The current rate meets or exceeds the adjusted break-even target. Validate reserve and customer-impact policy before locking it in."));
    }

    private void AddScenarioRecommendation(List<RecommendationItem> recommendations)
    {
        if (WorkspaceState.ScenarioItems.Count == 0)
        {
            recommendations.Add(new RecommendationItem(
                "Add at least one scenario stressor",
                "Capture a capital, labor, or reserve adjustment so the recommendation reflects non-base operating pressure."));
            return;
        }

        recommendations.Add(new RecommendationItem(
            "Persist the active scenario",
            "Save the current scenario state to Aurora so the adjusted recommendation is auditable and reproducible."));
    }

    private static void AddCustomerMixRecommendation(List<RecommendationItem> recommendations)
    {
        recommendations.Add(new RecommendationItem(
            "Review filtered customer mix",
            "Validate that customer filters reflect the service population you expect before using the workspace outputs in a production rate packet."));
    }

    private void BeginChatSubmission(string question)
    {
        IsChatBusy = true;
        ChatQuestion = question;
        ChatRuntimeStatusTitle = "Awaiting Jarvis response";
        ChatRuntimeStatusDetail = "Checking the live workspace context and streaming the reply into the assist rail.";
    }

    private async Task SubmitPromptCoreAsync(string question, AssistViewPromptRequestedEventArgs? args)
    {
        AppendUserMessage(question);

        var response = await AiApi.AskAsync(BuildChatRequest(question)).ConfigureAwait(false);
        UpdateChatRuntimeStatus(response.UsedFallback, response.AnswerSource);

        ApplyPromptResponse(question, response, args);
        await LoadRecommendationHistoryAsync(force: true).ConfigureAwait(false);
    }

    private WorkspaceChatRequest BuildChatRequest(string question)
    {
        return new WorkspaceChatRequest(
            question,
            BuildChatContextSummary(),
            WorkspaceState.SelectedEnterprise,
            WorkspaceState.SelectedFiscalYear)
        {
            ConversationHistory = BuildConversationHistory()
        };
    }

    private void ApplyPromptResponse(string question, WorkspaceChatResponse response, AssistViewPromptRequestedEventArgs? args)
    {
        AppendPromptTranscript(question, response);
        ApplyPromptAnswer(response);
        ApplyPromptProfile(response);
        ApplyPromptConversation(response);
        ApplyPromptResponseArgument(response, args);
        ChatQuestion = string.Empty;
    }

    private void HandleChatSubmissionFailure(Exception ex, AssistViewPromptRequestedEventArgs? args)
    {
        IsChatFallbackActive = true;
        ChatRuntimeStatusTitle = "AI runtime unavailable";
        ChatRuntimeStatusDetail = $"Jarvis reached the panel, but the server did not return a usable AI response. {ex.Message}";
        ChatAnswer = ex.Message;
        AppendAssistantMessage(ex.Message);

        if (args is not null)
        {
            args.Response = ex.Message;
        }
    }

    private void EndChatSubmission()
    {
        IsChatBusy = false;
    }

    private async Task OnAssistToolbarItemClicked(AssistViewToolbarItemClickedEventArgs args)
    {
        var action = args.Item?.Text;

        if (string.Equals(action, "Refresh Guidance", StringComparison.Ordinal))
        {
            await RefreshKnowledgeAsync(force: true);
            return;
        }

        if (string.Equals(action, "Reload History", StringComparison.Ordinal))
        {
            await LoadRecommendationHistoryAsync(force: true);
        }
    }

    private async Task OnAssistFooterToolbarItemClicked(AssistViewToolbarItemClickedEventArgs args)
    {
        var action = args.Item?.Text;

        if (string.Equals(action, "Ask Jarvis", StringComparison.Ordinal))
        {
            await SubmitFooterPromptAsync();
            return;
        }

        if (string.Equals(action, "Reset Thread", StringComparison.Ordinal))
        {
            await ResetChatAsync();
            return;
        }

        if (string.Equals(action, "Refresh Context", StringComparison.Ordinal))
        {
            await RefreshKnowledgeAsync(force: true);
        }
    }

    private void BeginKnowledgeRefresh()
    {
        IsKnowledgeBusy = true;
        KnowledgeStatus = "Refreshing live workspace guidance...";
    }

    private async Task RefreshKnowledgeCoreAsync(WorkspaceKnowledgeApiService knowledgeApi, string fingerprint)
    {
        var knowledge = await knowledgeApi.GetAsync(new WorkspaceKnowledgeRequest(WorkspaceState.ToBootstrapData())).ConfigureAwait(false);
        workspaceKnowledge = knowledge;
        lastKnowledgeFingerprint = fingerprint;
        ApplyKnowledge(knowledge);
    }

    private void ApplyKnowledgeFallback(string status, string fingerprint)
    {
        workspaceKnowledge = null;
        lastKnowledgeFingerprint = fingerprint;
        ApplyFallbackKnowledge(status);
    }

    private void HandleKnowledgeRefreshFailure(Exception ex, string fingerprint)
    {
        Console.WriteLine($"[jarvis-knowledge] Using deterministic fallback guidance: {ex.Message}");
        ApplyKnowledgeFallback($"Live guidance unavailable: {ex.Message}", fingerprint);
    }

    private void EndKnowledgeRefresh()
    {
        IsKnowledgeBusy = false;
    }

    private bool ShouldSkipKnowledgeRefresh(bool force, string fingerprint)
    {
        return !force && string.Equals(lastKnowledgeFingerprint, fingerprint, StringComparison.Ordinal);
    }

    private bool TryHandleKnowledgeRefreshPreconditions(bool force, string fingerprint, out WorkspaceKnowledgeApiService? knowledgeApi)
    {
        knowledgeApi = null;

        if (ShouldSkipKnowledgeRefresh(force, fingerprint))
        {
            return true;
        }

        if (TryApplyKnowledgeFallbackForMissingSelection(fingerprint))
        {
            return true;
        }

        return TryHandleMissingKnowledgeApiService(fingerprint, out knowledgeApi);
    }

    private bool TryHandleMissingKnowledgeApiService(string fingerprint, out WorkspaceKnowledgeApiService? knowledgeApi)
    {
        if (TryGetKnowledgeApiService(out knowledgeApi))
        {
            return false;
        }

        ApplyKnowledgeFallback("Live guidance unavailable: Workspace knowledge service is not registered for this host.", fingerprint);
        return true;
    }

    private bool TryApplyKnowledgeFallbackForMissingSelection(string fingerprint)
    {
        if (!string.IsNullOrWhiteSpace(WorkspaceState.SelectedEnterprise) && WorkspaceState.SelectedFiscalYear > 0)
        {
            return false;
        }

        ApplyKnowledgeFallback("Select an enterprise and fiscal year to load live workspace guidance.", fingerprint);
        return true;
    }

    private bool TryGetKnowledgeApiService(out WorkspaceKnowledgeApiService? knowledgeApi)
    {
        knowledgeApi = ServiceProvider.GetService<WorkspaceKnowledgeApiService>();
        return knowledgeApi is not null;
    }

    private void AppendPromptTranscript(string question, WorkspaceChatResponse response)
    {
        AppendAssistantMessage(response.Answer);
        chatPrompts.Add(new AssistViewPrompt
        {
            Prompt = question,
            Response = response.Answer,
            IsResponseHelpful = null
        });
    }

    private void ApplyPromptAnswer(WorkspaceChatResponse response)
    {
        ChatAnswer = response.Answer;
    }

    private void ApplyPromptProfile(WorkspaceChatResponse response)
    {
        CurrentUserLabel = string.IsNullOrWhiteSpace(response.UserDisplayName) ? CurrentUserLabel : response.UserDisplayName;
        CurrentProfileSummary = string.IsNullOrWhiteSpace(response.UserProfileSummary) ? CurrentProfileSummary : response.UserProfileSummary;
    }

    private void ApplyPromptConversation(WorkspaceChatResponse response)
    {
        CurrentConversationLabel = !string.IsNullOrWhiteSpace(response.ConversationId)
            ? $"Conversation {response.ConversationId} ({response.ConversationMessageCount} messages)"
            : CurrentConversationLabel;
    }

    private void ApplyPromptResponseArgument(WorkspaceChatResponse response, AssistViewPromptRequestedEventArgs? args)
    {
        if (args is not null)
        {
            args.Response = response.Answer;
        }
    }

    private static string FormatRecommendationTimestamp(string createdAtUtc)
    {
        return DateTimeOffset.Parse(createdAtUtc, CultureInfo.InvariantCulture).ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
    }

    private decimal GetFirstProjectionRateValue()
    {
        return WorkspaceState.ProjectionSeries.FirstOrDefault()?.Rate ?? WorkspaceState.CurrentRate;
    }

    private decimal GetLastProjectionRateValue()
    {
        return WorkspaceState.ProjectionSeries.LastOrDefault()?.Rate ?? WorkspaceState.CurrentRate;
    }

    private async Task ExecuteChatOperationAsync(Action begin, Func<Task> operation, Action<Exception> handleFailure, Action complete)
    {
        begin();
        await InvokeAsync(StateHasChanged);

        try
        {
            await operation().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            handleFailure(ex);
        }
        finally
        {
            complete();
            await InvokeAsync(StateHasChanged);
        }
    }

    private void BeginChatReset()
    {
        IsChatBusy = true;
        ChatRuntimeStatusTitle = "Resetting Jarvis thread";
        ChatRuntimeStatusDetail = "Clearing the current conversation and rehydrating the rail for the active workspace scope.";
    }

    private async Task ResetChatCoreAsync()
    {
        await AiApi.ResetConversationAsync(new WorkspaceConversationResetRequest(
            BuildChatContextSummary(),
            WorkspaceState.SelectedEnterprise,
            WorkspaceState.SelectedFiscalYear)).ConfigureAwait(false);

        chatTranscript.Clear();
        chatPrompts.Clear();
        recommendationHistory.Clear();
        ChatAnswer = "Jarvis thread reset for the current workspace context.";
        CurrentConversationLabel = "Local session";
        CurrentProfileSummary = "Jarvis will ask a few setup questions on first contact.";
        ChatQuestion = string.Empty;
    }

    private void HandleChatResetFailure(Exception ex)
    {
        ChatAnswer = ex.Message;
    }

    private void EndChatReset()
    {
        IsChatBusy = false;
    }

    private static string GetFingerprintSelectedFiscalYear(WorkspaceBootstrapData snapshot)
    {
        return snapshot.SelectedFiscalYear.ToString(CultureInfo.InvariantCulture);
    }

    private static string GetFingerprintCurrentRate(WorkspaceBootstrapData snapshot)
    {
        return snapshot.CurrentRate?.ToString(CultureInfo.InvariantCulture) ?? "0";
    }

    private static string GetFingerprintTotalCosts(WorkspaceBootstrapData snapshot)
    {
        return snapshot.TotalCosts?.ToString(CultureInfo.InvariantCulture) ?? "0";
    }

    private static string GetFingerprintProjectedVolume(WorkspaceBootstrapData snapshot)
    {
        return snapshot.ProjectedVolume?.ToString(CultureInfo.InvariantCulture) ?? "0";
    }

    private static string GetFingerprintScenarioCostTotal(WorkspaceBootstrapData snapshot)
    {
        var scenarioCostTotal = snapshot.ScenarioItems?.Sum(item => item.Cost) ?? 0m;
        return scenarioCostTotal.ToString(CultureInfo.InvariantCulture);
    }

    private static string GetFingerprintScenarioCount(WorkspaceBootstrapData snapshot)
    {
        var scenarioCount = snapshot.ScenarioItems?.Count ?? 0;
        return scenarioCount.ToString(CultureInfo.InvariantCulture);
    }

    private decimal GetProjectionDrift()
    {
        return GetLastProjectionRateValue() - GetFirstProjectionRateValue();
    }

    public void Dispose()
    {
        if (workspaceChangedHandler is not null)
        {
            WorkspaceState.Changed -= workspaceChangedHandler;
        }
        GC.SuppressFinalize(this);
    }

    public sealed record InsightCard(string Label, string Value, string Description);

    public sealed record RecommendationItem(string Title, string Description);
}