using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using System.Globalization;
using Syncfusion.Blazor.InteractiveChat;
using WileyCoWeb.Contracts;
using WileyCoWeb.Services;
using WileyCoWeb.State;

namespace WileyCoWeb.Components;

public partial class JarvisChatPanel : ComponentBase, IDisposable
{
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
    private WorkspaceKnowledgeResponse? workspaceKnowledge;

    [Inject]
    protected WorkspaceState WorkspaceState { get; set; } = default!;

    [Inject]
    protected WorkspaceAiApiService AiApi { get; set; } = default!;

    [Inject]
    protected IServiceProvider ServiceProvider { get; set; } = default!;

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

        await LoadRecommendationHistoryAsync().ConfigureAwait(false); // Persists via EfConversationRepository + UserContextPlugin (history now auditable)
        await RefreshKnowledgeAsync(force: true).ConfigureAwait(false);
        await InvokeAsync(StateHasChanged);
    }

    protected Task RefreshKnowledgeFromButtonAsync() => RefreshKnowledgeAsync(force: true);

    protected async Task OnPromptRequestedAsync(AssistViewPromptRequestedEventArgs args)
        => await SubmitPromptAsync(string.IsNullOrWhiteSpace(args?.Prompt) ? ChatQuestion : args.Prompt.Trim(), args);

    public Task AskChatAsync()
    {
        return SubmitPromptAsync(ChatQuestion);
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

    private async Task LoadRecommendationHistoryAsync()
    {
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
        }
        catch (Exception ex)
        {
            RecommendationHistoryStatus = $"Recommendation history is unavailable right now: {ex.Message}";
        }
    }

    private void UpdateChatRuntimeStatus(bool usedFallback)
    {
        IsChatFallbackActive = usedFallback;

        if (usedFallback)
        {
            ChatRuntimeStatusTitle = "AI runtime unavailable";
            ChatRuntimeStatusDetail = "Jarvis is reaching the server, but the server is returning fallback guidance instead of a live xAI or Semantic Kernel response. Check xAI key and endpoint configuration.";
            return;
        }

        ChatRuntimeStatusTitle = "Live AI available";
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

    private InsightCard CreateProjectionDriftInsight() => new(
        "Projection drift",
        GetProjectionDrift().ToString("C2"),
        "Difference between the first and last projected rates in the trend series.");

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
    }

    private async Task SubmitPromptCoreAsync(string question, AssistViewPromptRequestedEventArgs? args)
    {
        AppendUserMessage(question);

        var response = await AiApi.AskAsync(BuildChatRequest(question)).ConfigureAwait(false);
        UpdateChatRuntimeStatus(response.UsedFallback);

        ApplyPromptResponse(question, response, args);
        await LoadRecommendationHistoryAsync().ConfigureAwait(false);
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

    private decimal GetProjectionDrift() => GetLastProjectionRateValue() - GetFirstProjectionRateValue();

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