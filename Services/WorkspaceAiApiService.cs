using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using WileyCoWeb.Contracts;

namespace WileyCoWeb.Services;

public sealed class WorkspaceAiApiService(HttpClient httpClient, ILogger<WorkspaceAiApiService>? logger = null)
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
	{
		PropertyNameCaseInsensitive = true
	};

	public async Task<WorkspaceChatResponse> AskAsync(WorkspaceChatRequest request, CancellationToken cancellationToken = default)
	{
		logger?.LogInformation("Requesting workspace AI response for {Enterprise} FY {FiscalYear} (question length {QuestionLength}).", request.SelectedEnterprise, request.SelectedFiscalYear, request.Question?.Length ?? 0);
		var response = await httpClient.PostAsJsonAsync("api/ai/chat", request, JsonOptions, cancellationToken).ConfigureAwait(false);
		var payload = await response.Content.ReadFromJsonAsync<WorkspaceChatResponse>(JsonOptions, cancellationToken).ConfigureAwait(false);

		if (!response.IsSuccessStatusCode)
		{
			var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
			logger?.LogWarning("Workspace AI request failed with status {StatusCode}", (int)response.StatusCode);
			throw new InvalidOperationException(string.IsNullOrWhiteSpace(responseBody)
				? $"Workspace AI request failed with status {(int)response.StatusCode}."
				: responseBody);
		}

		logger?.LogInformation("Workspace AI response received (usedFallback={UsedFallback}, answerSource={AnswerSource}, failureCode={FailureCode}, conversationId={ConversationId}, turnCount={TurnCount}).", payload?.UsedFallback ?? false, payload?.AnswerSource ?? "unknown", payload?.FailureCode ?? "none", payload?.ConversationId ?? "N/A", payload?.ConversationMessageCount ?? 0);
		return payload ?? throw new InvalidOperationException("The workspace AI response was empty.");
	}

	public async Task ResetConversationAsync(WorkspaceConversationResetRequest request, CancellationToken cancellationToken = default)
	{
		logger?.LogInformation("Resetting workspace AI conversation for {Enterprise} FY {FiscalYear}.", request.SelectedEnterprise, request.SelectedFiscalYear);
		var response = await httpClient.PostAsJsonAsync("api/ai/chat/reset", request, JsonOptions, cancellationToken).ConfigureAwait(false);

		if (!response.IsSuccessStatusCode)
		{
			var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
			logger?.LogWarning("Workspace AI conversation reset failed with status {StatusCode}", (int)response.StatusCode);
			throw new InvalidOperationException(string.IsNullOrWhiteSpace(responseBody)
				? $"Workspace AI conversation reset failed with status {(int)response.StatusCode}."
				: responseBody);
		}
	}

	public async Task<WorkspaceRecommendationHistoryResponse> GetRecommendationHistoryAsync(WorkspaceRecommendationHistoryRequest request, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(request);
		logger?.LogInformation("Loading recommendation history for {Enterprise} FY {FiscalYear} (limit {Limit}).", request.SelectedEnterprise, request.SelectedFiscalYear, request.Limit);

		var endpoint = $"api/ai/recommendations?enterprise={Uri.EscapeDataString(request.SelectedEnterprise)}&fiscalYear={request.SelectedFiscalYear}&limit={request.Limit}";
		var response = await httpClient.GetAsync(endpoint, cancellationToken).ConfigureAwait(false);
		var payload = await response.Content.ReadFromJsonAsync<WorkspaceRecommendationHistoryResponse>(JsonOptions, cancellationToken).ConfigureAwait(false);

		if (!response.IsSuccessStatusCode)
		{
			var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
			logger?.LogWarning("Workspace recommendation history request failed with status {StatusCode}", (int)response.StatusCode);
			throw new InvalidOperationException(string.IsNullOrWhiteSpace(responseBody)
				? $"Workspace recommendation history request failed with status {(int)response.StatusCode}."
				: responseBody);
		}

		return payload ?? new WorkspaceRecommendationHistoryResponse([]);
	}

	/// <summary>
	/// Fetches Jarvis/AI health (used to detect whether a real xAI key is active or fallbacks are in use).
	/// </summary>
	public async Task<JarvisHealthResponse?> GetHealthAsync(CancellationToken cancellationToken = default)
	{
		try
		{
			var payload = await httpClient.GetFromJsonAsync<JarvisHealthResponse>("api/ai/health", JsonOptions, cancellationToken).ConfigureAwait(false);
			return payload;
		}
		catch (Exception ex)
		{
			logger?.LogWarning(ex, "Failed to fetch AI health status.");
			return null;
		}
	}

	/// <summary>
	/// Development-only helper: sends an xAI API key to the local API so it can be persisted
	/// into your gitignored local settings for the next run (and injected for the current process where possible).
	/// This endpoint only exists when the API is running in Development.
	/// </summary>
	public async Task<bool> SetDevXaiApiKeyAsync(string apiKey, CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(apiKey))
			return false;

		try
		{
			var response = await httpClient.PostAsJsonAsync("api/dev/xai-key", new { ApiKey = apiKey.Trim() }, JsonOptions, cancellationToken).ConfigureAwait(false);
			return response.IsSuccessStatusCode;
		}
		catch (Exception ex)
		{
			logger?.LogWarning(ex, "Failed to send dev xAI key to local API.");
			return false;
		}
	}
}
