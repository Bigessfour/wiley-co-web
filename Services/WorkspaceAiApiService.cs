using System.Net;
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

    public Task<WorkspaceChatResponse> AskAsync(WorkspaceChatRequest request, CancellationToken cancellationToken = default)
        => ExecuteAskAsync(request, cancellationToken);

    public async Task ResetConversationAsync(WorkspaceConversationResetRequest request, CancellationToken cancellationToken = default)
    {
        logger?.LogInformation("Resetting workspace AI conversation for {Enterprise} FY {FiscalYear}.", request.SelectedEnterprise, request.SelectedFiscalYear);

        await httpClient.SendJsonAsync(
            HttpMethod.Post,
            "api/ai/chat/reset",
            request,
            JsonOptions,
            (statusCode, responseBody) => new InvalidOperationException(BuildFailureMessage("workspace AI conversation reset", statusCode, responseBody)),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<WorkspaceChatResponse> ExecuteAskAsync(WorkspaceChatRequest request, CancellationToken cancellationToken)
    {
        LogAskRequest(request);

        var payload = await SendAskRequestAsync(request, cancellationToken).ConfigureAwait(false);

        LogAskResponse(payload);
        return payload ?? throw new InvalidOperationException("The workspace AI response was empty.");
    }

    private void LogAskRequest(WorkspaceChatRequest request)
    {
        logger?.LogInformation(
            "Requesting workspace AI response for {Enterprise} FY {FiscalYear} (question length {QuestionLength}).",
            request.SelectedEnterprise,
            request.SelectedFiscalYear,
            GetQuestionLength(request));
    }

    private void LogAskResponse(WorkspaceChatResponse? payload)
    {
        logger?.LogInformation(
            "Workspace AI response received (usedFallback={UsedFallback}, conversationId={ConversationId}, turnCount={TurnCount}).",
            GetUsedFallback(payload),
            GetConversationId(payload),
            GetConversationMessageCount(payload));
    }

    private static bool GetUsedFallback(WorkspaceChatResponse? payload)
        => payload?.UsedFallback ?? false;

    private static int GetQuestionLength(WorkspaceChatRequest request)
        => request.Question?.Length ?? 0;

    private static string GetConversationId(WorkspaceChatResponse? payload)
        => payload?.ConversationId ?? "N/A";

    private static int GetConversationMessageCount(WorkspaceChatResponse? payload)
        => payload?.ConversationMessageCount ?? 0;

    private Task<WorkspaceChatResponse?> SendAskRequestAsync(WorkspaceChatRequest request, CancellationToken cancellationToken)
    {
        return httpClient.SendJsonAsync<WorkspaceChatResponse>(
            HttpMethod.Post,
            "api/ai/chat",
            request,
            JsonOptions,
            "The workspace AI response was not valid JSON.",
            CreateFailureException("workspace AI request"),
            cancellationToken);
    }

    public async Task<WorkspaceRecommendationHistoryResponse> GetRecommendationHistoryAsync(WorkspaceRecommendationHistoryRequest request, CancellationToken cancellationToken = default)
        => await ExecuteGetRecommendationHistoryAsync(request, cancellationToken).ConfigureAwait(false);

    private async Task<WorkspaceRecommendationHistoryResponse> ExecuteGetRecommendationHistoryAsync(WorkspaceRecommendationHistoryRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        LogRecommendationHistoryRequest(request);

        var payload = await LoadRecommendationHistoryAsync(request, cancellationToken).ConfigureAwait(false);

        LogRecommendationHistoryResponse(request);
        return payload ?? new WorkspaceRecommendationHistoryResponse([]);
    }

    private void LogRecommendationHistoryRequest(WorkspaceRecommendationHistoryRequest request)
    {
        logger?.LogInformation(
            "Loading recommendation history for {Enterprise} FY {FiscalYear} (limit {Limit}).",
            request.SelectedEnterprise,
            request.SelectedFiscalYear,
            request.Limit);
    }

    private void LogRecommendationHistoryResponse(WorkspaceRecommendationHistoryRequest request)
    {
        logger?.LogInformation(
            "Workspace recommendation history loaded for {Enterprise} FY {FiscalYear}.",
            request.SelectedEnterprise,
            request.SelectedFiscalYear);
    }

    private Task<WorkspaceRecommendationHistoryResponse?> LoadRecommendationHistoryAsync(
        WorkspaceRecommendationHistoryRequest request,
        CancellationToken cancellationToken)
    {
        var endpoint = BuildRecommendationHistoryEndpoint(request);
        return httpClient.GetJsonAsync<WorkspaceRecommendationHistoryResponse>(
            endpoint,
            JsonOptions,
            $"The workspace recommendation history response from {endpoint} was not valid JSON.",
            (statusCode, responseBody) => new InvalidOperationException(BuildFailureMessage("workspace recommendation history request", statusCode, responseBody)),
            cancellationToken);
    }

    private static string BuildRecommendationHistoryEndpoint(WorkspaceRecommendationHistoryRequest request)
        => $"api/ai/recommendations?enterprise={Uri.EscapeDataString(request.SelectedEnterprise)}&fiscalYear={request.SelectedFiscalYear}&limit={request.Limit}";

    private static string BuildFailureMessage(string operationName, HttpStatusCode statusCode, string? responseBody)
    {
        var detail = HttpProblemDetailsParser.ExtractMessage(responseBody);
        return string.IsNullOrWhiteSpace(detail)
            ? $"{operationName} failed with status {(int)statusCode}."
            : $"{operationName} failed with status {(int)statusCode}: {detail}";
    }

    private static Func<HttpStatusCode, string?, Exception> CreateFailureException(string operationName)
        => (statusCode, responseBody) => new InvalidOperationException(BuildFailureMessage(operationName, statusCode, responseBody));
}
