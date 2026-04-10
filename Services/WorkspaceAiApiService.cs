using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using WileyCoWeb.Contracts;

namespace WileyCoWeb.Services;

public sealed class WorkspaceAiApiService
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
	{
		PropertyNameCaseInsensitive = true
	};

	private readonly HttpClient httpClient;
    private readonly ILogger<WorkspaceAiApiService>? logger;

    public WorkspaceAiApiService(HttpClient httpClient, ILogger<WorkspaceAiApiService>? logger = null)
	{
		this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        this.logger = logger;
	}

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

		logger?.LogInformation("Workspace AI response received (usedFallback={UsedFallback}, conversationId={ConversationId}, turnCount={TurnCount}).", payload?.UsedFallback ?? false, payload?.ConversationId ?? "N/A", payload?.ConversationMessageCount ?? 0);
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
}
