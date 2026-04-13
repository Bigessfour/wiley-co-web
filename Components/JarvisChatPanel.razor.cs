using Microsoft.AspNetCore.Components;
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

    [Inject]
    protected WorkspaceState WorkspaceState { get; set; } = default!;

    [Inject]
    protected WorkspaceAiApiService AiApi { get; set; } = default!;

    protected List<AssistViewPrompt> ChatPrompts => chatPrompts;
    public List<string> PromptSuggestions => promptSuggestions;
    protected string StatusText => GetStatusText();
    protected string PrimaryBrief => BuildPrimaryBrief();
    protected IReadOnlyList<InsightCard> Insights => BuildInsights();
    protected IReadOnlyList<RecommendationItem> RecommendedActions => BuildRecommendations();
    public IReadOnlyList<WorkspaceRecommendationHistoryItem> RecommendationHistory => recommendationHistory;
    protected IReadOnlyList<WorkspaceChatMessage> ChatTranscript => chatTranscript;
    protected string ChatQuestion { get; set; } = "What should I know about the current workspace?";
    protected string ChatAnswer { get; set; } = "Ask Jarvis a question about the workspace, codebase, or AI tools.";
    protected string ChatContextSummary => BuildChatContextSummary();
    protected string RecommendationHistoryStatus { get; set; } = "Recommendation history will appear here after Jarvis saves a recommendation.";
    protected string CurrentUserLabel { get; set; } = "Guest";
    protected string CurrentConversationLabel { get; set; } = "Local session";
    protected string CurrentProfileSummary { get; set; } = "Jarvis will ask a few setup questions on first contact.";
    protected bool IsChatBusy { get; set; }
    protected bool CanAskChat => !IsChatBusy && !string.IsNullOrWhiteSpace(ChatQuestion);
    public bool IsSecureJarvisEnabled => !string.Equals(Environment.GetEnvironmentVariable("UI__UseSecureJarvis"), "false", StringComparison.OrdinalIgnoreCase);

    protected override void OnInitialized()
    {
        workspaceChangedHandler = () => _ = InvokeAsync(StateHasChanged);
        WorkspaceState.Changed += workspaceChangedHandler;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        await LoadRecommendationHistoryAsync().ConfigureAwait(false); // Persists via EfConversationRepository + UserContextPlugin (history now auditable)
        await InvokeAsync(StateHasChanged);
    }

    protected async Task OnPromptRequestedAsync(AssistViewPromptRequestedEventArgs args)
    {
        if (args is null)
        {
            return;
        }

        var question = string.IsNullOrWhiteSpace(args.Prompt) ? ChatQuestion : args.Prompt.Trim();
        if (string.IsNullOrWhiteSpace(question))
        {
            args.Cancel = true;
            return;
        }

        await SubmitPromptAsync(question, args);
    }

    public Task AskChatAsync()
    {
        return SubmitPromptAsync(ChatQuestion);
    }

    public async Task ResetChatAsync()
    {
        try
        {
            IsChatBusy = true;
            await InvokeAsync(StateHasChanged);

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
        catch (Exception ex)
        {
            ChatAnswer = ex.Message;
        }
        finally
        {
            IsChatBusy = false;
            await InvokeAsync(StateHasChanged);
        }
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

        try
        {
            IsChatBusy = true;
            ChatQuestion = question;
            await InvokeAsync(StateHasChanged);

            AppendUserMessage(question);

            var response = await AiApi.AskAsync(new WorkspaceChatRequest(
                question,
                BuildChatContextSummary(),
                WorkspaceState.SelectedEnterprise,
                WorkspaceState.SelectedFiscalYear)
            {
                ConversationHistory = BuildConversationHistory()
            }).ConfigureAwait(false);

            AppendAssistantMessage(response.Answer);
            chatPrompts.Add(new AssistViewPrompt
            {
                Prompt = question,
                Response = response.Answer,
                IsResponseHelpful = null
            });
            ChatAnswer = response.Answer;
            CurrentUserLabel = string.IsNullOrWhiteSpace(response.UserDisplayName) ? CurrentUserLabel : response.UserDisplayName;
            CurrentProfileSummary = string.IsNullOrWhiteSpace(response.UserProfileSummary) ? CurrentProfileSummary : response.UserProfileSummary;
            CurrentConversationLabel = !string.IsNullOrWhiteSpace(response.ConversationId)
                ? $"Conversation {response.ConversationId} ({response.ConversationMessageCount} messages)"
                : CurrentConversationLabel;

            await LoadRecommendationHistoryAsync().ConfigureAwait(false);

            if (args is not null)
            {
                args.Response = response.Answer;
            }

            ChatQuestion = string.Empty;
        }
        catch (Exception ex)
        {
            ChatAnswer = ex.Message;
            AppendAssistantMessage(ex.Message);
            if (args is not null)
            {
                args.Response = ex.Message;
            }
        }
        finally
        {
            IsChatBusy = false;
            await InvokeAsync(StateHasChanged);
        }
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
        var rateGap = WorkspaceState.AdjustedRecommendedRate - WorkspaceState.CurrentRate;
        return $"{WorkspaceState.ContextSummary}; rate gap {rateGap:C2}; scenario costs {WorkspaceState.ScenarioCostTotal:C0}; customers {WorkspaceState.FilteredCustomerCount}.";
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

            RecommendationHistoryStatus = recommendationHistory.Count == 0
                ? "No saved recommendations yet for this workspace scope."
                : $"Loaded {recommendationHistory.Count} saved recommendation{(recommendationHistory.Count == 1 ? string.Empty : "s")} for this workspace scope.";
        }
        catch (Exception ex)
        {
            RecommendationHistoryStatus = $"Recommendation history is unavailable right now: {ex.Message}";
        }
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
        var adjustedBreakEven = WorkspaceState.AdjustedRecommendedRate;
        var rateGap = adjustedBreakEven - WorkspaceState.CurrentRate;
        var customerCount = WorkspaceState.FilteredCustomerCount;
        var firstProjection = WorkspaceState.ProjectionSeries.FirstOrDefault()?.Rate ?? WorkspaceState.CurrentRate;
        var lastProjection = WorkspaceState.ProjectionSeries.LastOrDefault()?.Rate ?? WorkspaceState.CurrentRate;
        var projectionDelta = lastProjection - firstProjection;

        return
        [
            new InsightCard(
                "Rate gap",
                rateGap.ToString("C2"),
                rateGap > 0 ? "Positive values indicate the rate is below the adjusted break-even target." : "Negative values indicate coverage above the adjusted break-even target."),
            new InsightCard(
                "Scenario pressure",
                WorkspaceState.ScenarioCostTotal.ToString("C0"),
                "Combined impact of all active scenario items on the current workspace."),
            new InsightCard(
                "Customer scope",
                customerCount.ToString(),
                "Filtered customer records currently contributing to the viewer and service mix review."),
            new InsightCard(
                "Projection drift",
                projectionDelta.ToString("C2"),
                "Difference between the first and last projected rates in the trend series.")
        ];
    }

    private List<RecommendationItem> BuildRecommendations()
    {
        var recommendations = new List<RecommendationItem>();
        var adjustedBreakEven = WorkspaceState.AdjustedRecommendedRate;
        var rateGap = adjustedBreakEven - WorkspaceState.CurrentRate;

        if (rateGap > 0)
        {
            recommendations.Add(new RecommendationItem(
                "Close the modeled rate gap",
                $"Increase the working rate by {rateGap:C2} or offset the same amount through cost reductions before finalizing the scenario."));
        }
        else
        {
            recommendations.Add(new RecommendationItem(
                "Preserve current coverage",
                "The current rate meets or exceeds the adjusted break-even target. Validate reserve and customer-impact policy before locking it in."));
        }

        if (WorkspaceState.ScenarioItems.Count == 0)
        {
            recommendations.Add(new RecommendationItem(
                "Add at least one scenario stressor",
                "Capture a capital, labor, or reserve adjustment so the recommendation reflects non-base operating pressure."));
        }
        else
        {
            recommendations.Add(new RecommendationItem(
                "Persist the active scenario",
                "Save the current scenario state to Aurora so the adjusted recommendation is auditable and reproducible."));
        }

        recommendations.Add(new RecommendationItem(
            "Review filtered customer mix",
            "Validate that customer filters reflect the service population you expect before using the workspace outputs in a production rate packet."));

        return recommendations;
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