using Microsoft.AspNetCore.Components;
using WileyCoWeb.Contracts;
using WileyCoWeb.Services;
using WileyCoWeb.State;

namespace WileyCoWeb.Components;

public partial class JarvisChatPanel : ComponentBase, IDisposable
{
    private readonly List<WorkspaceChatMessage> chatTranscript = [];

    [Inject]
    protected WorkspaceState WorkspaceState { get; set; } = default!;

    [Inject]
    protected WorkspaceAiApiService AiApi { get; set; } = default!;

    protected string StatusText => GetStatusText();
    protected string PrimaryBrief => BuildPrimaryBrief();
    protected IReadOnlyList<InsightCard> Insights => BuildInsights();
    protected IReadOnlyList<RecommendationItem> RecommendedActions => BuildRecommendations();
    protected IReadOnlyList<WorkspaceChatMessage> ChatTranscript => chatTranscript;
    protected string ChatQuestion { get; set; } = "What should I know about the current workspace?";
    protected string ChatAnswer { get; set; } = "Ask Jarvis a question about the workspace, codebase, or AI tools.";
    protected string ChatContextSummary => BuildChatContextSummary();
    protected string CurrentUserLabel { get; set; } = "Guest";
    protected string CurrentConversationLabel { get; set; } = "Local session";
    protected bool IsChatBusy { get; set; }
    protected bool CanAskChat => !IsChatBusy && !string.IsNullOrWhiteSpace(ChatQuestion);

    protected override void OnInitialized()
    {
        WorkspaceState.Changed += HandleWorkspaceChanged;
    }

    protected void RefreshPanel()
    {
        StateHasChanged();
    }

    protected async Task AskChatAsync()
    {
        var question = ChatQuestion?.Trim();
        if (string.IsNullOrWhiteSpace(question))
        {
            ChatAnswer = "Enter a question before asking Jarvis.";
            await InvokeAsync(StateHasChanged);
            return;
        }

        try
        {
            IsChatBusy = true;
            await InvokeAsync(StateHasChanged);

            var response = await AiApi.AskAsync(new WorkspaceChatRequest(
                question,
                BuildChatContextSummary(),
                WorkspaceState.SelectedEnterprise,
                WorkspaceState.SelectedFiscalYear)
            {
                ConversationHistory = BuildConversationHistory()
            }).ConfigureAwait(false);

            chatTranscript.Add(new WorkspaceChatMessage("user", question));
            chatTranscript.Add(new WorkspaceChatMessage("assistant", response.Answer));
            ChatAnswer = response.Answer;
            CurrentUserLabel = string.IsNullOrWhiteSpace(response.UserDisplayName) ? CurrentUserLabel : response.UserDisplayName;
            CurrentConversationLabel = !string.IsNullOrWhiteSpace(response.ConversationId)
                ? $"Conversation {response.ConversationId} ({response.ConversationMessageCount} messages)"
                : CurrentConversationLabel;
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

    protected Task ClearChatAsync()
    {
        ChatQuestion = "What should I know about the current workspace?";
        ChatAnswer = "Ask Jarvis a question about the workspace, codebase, or AI tools.";
        chatTranscript.Clear();
        return InvokeAsync(StateHasChanged);
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

    protected bool IsAssistantMessage(string role)
    {
        return string.Equals(role, "assistant", StringComparison.OrdinalIgnoreCase) || string.Equals(role, "jarvis", StringComparison.OrdinalIgnoreCase);
    }

    private IReadOnlyList<WorkspaceChatMessage> BuildConversationHistory()
    {
        if (chatTranscript.Count == 0)
        {
            return [];
        }

        return chatTranscript.TakeLast(12).ToArray();
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

    private IReadOnlyList<RecommendationItem> BuildRecommendations()
    {
        var recommendations = new List<RecommendationItem>();
        var adjustedBreakEven = WorkspaceState.AdjustedRecommendedRate;
        var rateGap = adjustedBreakEven - WorkspaceState.CurrentRate;

        if (rateGap > 0)
        {
            recommendations.Add(new RecommendationItem(
                "Close the modeled rate gap",
                $"Increase the working rate by {rateGap.ToString("C2")} or offset the same amount through cost reductions before finalizing the scenario."));
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

    private void HandleWorkspaceChanged()
    {
        _ = InvokeAsync(StateHasChanged);
    }

    public void Dispose()
    {
        WorkspaceState.Changed -= HandleWorkspaceChanged;
    }

    protected sealed record InsightCard(string Label, string Value, string Description);

    protected sealed record RecommendationItem(string Title, string Description);
}