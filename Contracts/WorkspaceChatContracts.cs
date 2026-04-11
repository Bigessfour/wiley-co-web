namespace WileyCoWeb.Contracts;

public sealed record WorkspaceChatRequest(
	string Question,
	string ContextSummary,
	string SelectedEnterprise,
	int SelectedFiscalYear)
{
	public IReadOnlyList<WorkspaceChatMessage>? ConversationHistory { get; init; }
}

public sealed record WorkspaceChatMessage(string Role, string Content);

public sealed record WorkspaceChatResponse(
	string Question,
	string Answer,
	bool UsedFallback,
	string ContextSummary)
{
	public string? UserDisplayName { get; init; }
	public string? UserProfileSummary { get; init; }
	public string? ConversationId { get; init; }
	public int ConversationMessageCount { get; init; }
	public bool IsFirstConversation { get; init; }
	public bool CanResetConversation { get; init; }
}

public sealed record WorkspaceUserContext(
	string UserId,
	string DisplayName,
	string? Email,
	string PreferencesSummary);

public sealed record WorkspaceConversationResetRequest(
	string ContextSummary,
	string SelectedEnterprise,
	int SelectedFiscalYear);

public sealed record WorkspaceRecommendationHistoryRequest(
	string SelectedEnterprise,
	int SelectedFiscalYear,
	int Limit = 12);

public sealed record WorkspaceRecommendationHistoryItem(
	string RecommendationId,
	string ConversationId,
	string UserDisplayName,
	string Question,
	string Recommendation,
	bool UsedFallback,
	string CreatedAtUtc);

public sealed record WorkspaceRecommendationHistoryResponse(
	IReadOnlyList<WorkspaceRecommendationHistoryItem> Items);
