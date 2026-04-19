using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using WileyCoWeb.Contracts;

namespace WileyCoWeb.Services;

public sealed class WorkspaceKnowledgeApiService(HttpClient httpClient, ILogger<WorkspaceKnowledgeApiService>? logger = null)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    public async Task<WorkspaceKnowledgeResponse> GetAsync(WorkspaceKnowledgeRequest request, CancellationToken cancellationToken = default)
        => await GetAsyncCore(request, cancellationToken).ConfigureAwait(false);

    private async Task<WorkspaceKnowledgeResponse> GetAsyncCore(WorkspaceKnowledgeRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        LogKnowledgeRequest(request);
        var payload = await SendKnowledgeRequestAsync(request, cancellationToken).ConfigureAwait(false);
        var response = EnsureKnowledgeResponse(payload);
        LogKnowledgeResponse(response);
        return response;
    }

    private void LogKnowledgeRequest(WorkspaceKnowledgeRequest request)
    {
        logger?.LogInformation(
            "Requesting workspace knowledge for {Enterprise} FY {FiscalYear}",
            request.Snapshot.SelectedEnterprise,
            request.Snapshot.SelectedFiscalYear);
    }

    private async Task<WorkspaceKnowledgeResponse?> SendKnowledgeRequestAsync(WorkspaceKnowledgeRequest request, CancellationToken cancellationToken)
    {
        return await httpClient.SendJsonAsync<WorkspaceKnowledgeResponse>(
            HttpMethod.Post,
            "api/workspace/knowledge",
            request,
            JsonOptions,
            "The workspace knowledge response was not valid JSON.",
            (statusCode, responseBody) => new InvalidOperationException(BuildFailureMessage(statusCode, responseBody)),
            cancellationToken).ConfigureAwait(false);
    }

    private static WorkspaceKnowledgeResponse EnsureKnowledgeResponse(WorkspaceKnowledgeResponse? payload)
    {
        return payload ?? throw new InvalidOperationException("The workspace knowledge response was empty.");
    }

    private void LogKnowledgeResponse(WorkspaceKnowledgeResponse payload)
    {
        logger?.LogInformation(
            "Workspace knowledge loaded for {Enterprise} FY {FiscalYear}",
            payload.SelectedEnterprise,
            payload.SelectedFiscalYear);
    }

    private static string BuildFailureMessage(HttpStatusCode statusCode, string? responseBody)
    {
        var detail = HttpProblemDetailsParser.ExtractMessage(responseBody);
        return string.IsNullOrWhiteSpace(detail)
            ? $"Loading workspace knowledge failed with status {(int)statusCode}."
            : $"Loading workspace knowledge failed with status {(int)statusCode}: {detail}";
    }
}