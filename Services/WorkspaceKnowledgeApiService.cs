using System.Net;
using System.Net.Http.Json;
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
    {
        ArgumentNullException.ThrowIfNull(request);

        using var response = await httpClient.PostAsJsonAsync("api/workspace/knowledge", request, JsonOptions, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            logger?.LogWarning("Workspace knowledge request failed with status {StatusCode}", (int)response.StatusCode);
            throw new InvalidOperationException(BuildFailureMessage(response.StatusCode, responseBody));
        }

        var payload = await response.Content.ReadFromJsonAsync<WorkspaceKnowledgeResponse>(JsonOptions, cancellationToken).ConfigureAwait(false);
        if (payload is null)
        {
            throw new InvalidOperationException("The workspace knowledge response was empty.");
        }

        return payload;
    }

    private static string BuildFailureMessage(HttpStatusCode statusCode, string? responseBody)
    {
        var detail = ExtractFailureDetail(responseBody);
        return string.IsNullOrWhiteSpace(detail)
            ? $"Loading workspace knowledge failed with status {(int)statusCode}."
            : $"Loading workspace knowledge failed with status {(int)statusCode}: {detail}";
    }

    private static string? ExtractFailureDetail(string? responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(responseBody);
            var root = document.RootElement;

            if (root.ValueKind == JsonValueKind.String)
            {
                return root.GetString();
            }

            if (root.ValueKind == JsonValueKind.Object)
            {
                if (root.TryGetProperty("detail", out var detailElement) && detailElement.ValueKind == JsonValueKind.String)
                {
                    var detail = detailElement.GetString();
                    if (!string.IsNullOrWhiteSpace(detail))
                    {
                        return detail;
                    }
                }

                if (root.TryGetProperty("title", out var titleElement) && titleElement.ValueKind == JsonValueKind.String)
                {
                    var title = titleElement.GetString();
                    if (!string.IsNullOrWhiteSpace(title))
                    {
                        return title;
                    }
                }
            }
        }
        catch (JsonException)
        {
        }

        return responseBody.Trim();
    }
}