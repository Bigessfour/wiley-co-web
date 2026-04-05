using Microsoft.AspNetCore.Components;
using WileyCoWeb.State;

namespace WileyCoWeb.Components;

public partial class JarvisChatPanel : ComponentBase
{
    [Inject]
    protected WorkspaceState WorkspaceState { get; set; } = default!;

    private readonly List<JarvisChatMessage> messages = new();

    protected string DraftMessage { get; set; } = "What rate do we need if trash costs rise 5%?";
    protected string SuggestedPrompt => $"Ask JARVIS about {WorkspaceState.ContextSummary}, break-even math, customer mix, or scenario impact.";
    protected string StatusText { get; set; } = "Ready";
    protected bool IsTyping { get; set; }

    protected IReadOnlyList<JarvisChatMessage> Messages => messages;

    protected override void OnInitialized()
    {
        messages.Add(JarvisChatMessage.Assistant($"I’m ready to analyze {WorkspaceState.ContextSummary}. Select an enterprise and ask a question."));
    }

    protected async Task SendMessageAsync()
    {
        var prompt = DraftMessage?.Trim();
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return;
        }

        messages.Add(JarvisChatMessage.User(prompt));
        IsTyping = true;
        StatusText = "Analyzing current context";
        StateHasChanged();

        await Task.Delay(250);

        var response = BuildLocalResponse(prompt);
        messages.Add(JarvisChatMessage.Assistant(response));

        DraftMessage = string.Empty;
        IsTyping = false;
        StatusText = "Ready";
    }

    protected Task ClearConversationAsync()
    {
        messages.Clear();
        messages.Add(JarvisChatMessage.Assistant("Conversation cleared. Ask me for a rate or scenario analysis."));
        StatusText = "Ready";
        DraftMessage = string.Empty;
        return Task.CompletedTask;
    }

    private string BuildLocalResponse(string prompt)
    {
        var normalized = prompt.ToLowerInvariant();

        if (normalized.Contains("trash") || normalized.Contains("truck"))
        {
            return $"Trash pricing should move upward enough to absorb the added truck cost while preserving the reserve target for {WorkspaceState.ContextSummary}. The next step is to plug the actual expense into the scenario planner and recalculate the break-even rate.";
        }

        if (normalized.Contains("sewer") || normalized.Contains("water"))
        {
            return $"The selected utility should be compared against projected volume, operating costs, and reserve policy for {WorkspaceState.ContextSummary}. Once the Aurora-backed data is connected, JARVIS can calculate the exact break-even adjustment.";
        }

        return $"I can explain the rate impact once enterprise totals, volume, and scenario cost inputs are connected to the API for {WorkspaceState.ContextSummary}. For now, this panel is the secure chat scaffold for the future Grok endpoint.";
    }

    protected sealed record JarvisChatMessage(string Author, string Text, bool IsUser, DateTime Timestamp)
    {
        public static JarvisChatMessage User(string text) => new("You", text, true, DateTime.UtcNow);

        public static JarvisChatMessage Assistant(string text) => new("JARVIS", text, false, DateTime.UtcNow);
    }
}