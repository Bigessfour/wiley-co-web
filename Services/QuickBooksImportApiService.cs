using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using WileyCoWeb.Contracts;

namespace WileyCoWeb.Services;

public sealed class QuickBooksImportApiService(HttpClient httpClient, ILogger<QuickBooksImportApiService>? logger = null)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public Task<QuickBooksImportPreviewResponse> PreviewAsync(
        byte[] fileBytes,
        string fileName,
        string selectedEnterprise,
        int selectedFiscalYear,
        CancellationToken cancellationToken = default)
        => PostAsync<QuickBooksImportPreviewResponse>("api/imports/quickbooks/preview", fileBytes, fileName, selectedEnterprise, selectedFiscalYear, cancellationToken);

    public Task<QuickBooksImportCommitResponse> CommitAsync(
        byte[] fileBytes,
        string fileName,
        string selectedEnterprise,
        int selectedFiscalYear,
        CancellationToken cancellationToken = default)
        => PostAsync<QuickBooksImportCommitResponse>("api/imports/quickbooks/commit", fileBytes, fileName, selectedEnterprise, selectedFiscalYear, cancellationToken);

    public Task<QuickBooksImportGuidanceResponse> AskAsync(QuickBooksImportGuidanceRequest request, CancellationToken cancellationToken = default)
        => ExecuteAskAsync(request, cancellationToken);

    public Task<QuickBooksRoutingConfigurationResponse> GetRoutingConfigurationAsync(CancellationToken cancellationToken = default)
        => ExecuteGetAsync<QuickBooksRoutingConfigurationResponse>("api/imports/quickbooks/routing", "QuickBooks routing configuration", cancellationToken);

    public Task<QuickBooksRoutingConfigurationResponse> SaveRoutingConfigurationAsync(QuickBooksRoutingConfigurationRequest request, CancellationToken cancellationToken = default)
        => ExecuteSendJsonAsync<QuickBooksRoutingConfigurationResponse>(HttpMethod.Put, "api/imports/quickbooks/routing", request, "QuickBooks routing configuration update", cancellationToken);

    public Task<QuickBooksImportHistoryResponse> GetImportHistoryAsync(CancellationToken cancellationToken = default)
        => ExecuteGetAsync<QuickBooksImportHistoryResponse>("api/imports/quickbooks/history", "QuickBooks import history", cancellationToken);

    public Task<QuickBooksHistoricalRerouteResponse> ReapplyRoutingAsync(QuickBooksHistoricalRerouteRequest request, CancellationToken cancellationToken = default)
        => ExecuteSendJsonAsync<QuickBooksHistoricalRerouteResponse>(HttpMethod.Post, "api/imports/quickbooks/reroute", request, "QuickBooks historical reroute", cancellationToken);

    private async Task<TResponse> PostAsync<TResponse>(
        string requestUri,
        byte[] fileBytes,
        string fileName,
        string selectedEnterprise,
        int selectedFiscalYear,
        CancellationToken cancellationToken)
        => await ExecuteImportRequestAsync<TResponse>(new QuickBooksImportRequestContext(requestUri, fileBytes, fileName, selectedEnterprise, selectedFiscalYear), cancellationToken).ConfigureAwait(false);

    private async Task<QuickBooksImportGuidanceResponse> ExecuteAskAsync(QuickBooksImportGuidanceRequest request, CancellationToken cancellationToken)
    {
        LogAskRequest(request);

        var payload = await SendAskRequestAsync(request, cancellationToken).ConfigureAwait(false);

        LogAskResponse(payload);
        return payload ?? throw new InvalidOperationException("The QuickBooks import guidance response was empty.");
    }

    private void LogAskRequest(QuickBooksImportGuidanceRequest request)
    {
        logger?.LogInformation("Requesting QuickBooks import guidance for question length {QuestionLength}", GetQuestionLength(request));
    }

    private void LogAskResponse(QuickBooksImportGuidanceResponse? payload)
    {
        logger?.LogInformation("QuickBooks import guidance request completed successfully (usedFallback={UsedFallback}).", GetUsedFallback(payload));
    }

    private Task<QuickBooksImportGuidanceResponse?> SendAskRequestAsync(QuickBooksImportGuidanceRequest request, CancellationToken cancellationToken)
    {
        return httpClient.SendJsonAsync<QuickBooksImportGuidanceResponse>(
            HttpMethod.Post,
            "api/imports/quickbooks/assistant",
            request,
            JsonOptions,
            "The QuickBooks import guidance response was not valid JSON.",
            CreateFailureException("QuickBooks import assistance"),
            cancellationToken);
    }

    private async Task<TResponse> ExecuteImportRequestAsync<TResponse>(QuickBooksImportRequestContext context, CancellationToken cancellationToken)
    {
        LogImportRequest(context);

        using var form = CreateMultipartFormDataContent(context);

        var payload = await SendImportRequestAsync<TResponse>(context, form, cancellationToken).ConfigureAwait(false);

        LogImportRequestSucceeded(context.RequestUri);
        return payload ?? throw new InvalidOperationException("The QuickBooks import response was empty.");
    }

    private async Task<TResponse> ExecuteGetAsync<TResponse>(string requestUri, string operation, CancellationToken cancellationToken)
    {
        logger?.LogInformation("Requesting {Operation} from {RequestUri}", operation, requestUri);

        var payload = await httpClient.GetJsonAsync<TResponse>(
            requestUri,
            JsonOptions,
            $"The {operation} response was not valid JSON.",
            CreateFailureException(operation),
            cancellationToken).ConfigureAwait(false);

        return payload ?? throw new InvalidOperationException($"The {operation} response was empty.");
    }

    private async Task<TResponse> ExecuteSendJsonAsync<TResponse>(HttpMethod method, string requestUri, object requestBody, string operation, CancellationToken cancellationToken)
    {
        logger?.LogInformation("Posting {Operation} to {RequestUri}", operation, requestUri);

        var payload = await httpClient.SendJsonAsync<TResponse>(
            method,
            requestUri,
            requestBody,
            JsonOptions,
            $"The {operation} response was not valid JSON.",
            CreateFailureException(operation),
            cancellationToken).ConfigureAwait(false);

        return payload ?? throw new InvalidOperationException($"The {operation} response was empty.");
    }

    private void LogImportRequest(QuickBooksImportRequestContext context)
    {
        logger?.LogInformation(
            "Posting QuickBooks import request to {RequestUri} for file {FileName} ({ByteCount} bytes) in {Enterprise} FY {FiscalYear}",
            context.RequestUri,
            Path.GetFileName(context.FileName),
            context.FileBytes.LongLength,
            context.SelectedEnterprise,
            context.SelectedFiscalYear);
    }

    private void LogImportRequestSucceeded(string requestUri)
    {
        logger?.LogInformation("QuickBooks import request to {RequestUri} completed successfully.", requestUri);
    }

    private static MultipartFormDataContent CreateMultipartFormDataContent(QuickBooksImportRequestContext context)
    {
        var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(context.FileBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

        form.Add(fileContent, "file", context.FileName);
        form.Add(new StringContent(context.SelectedEnterprise), "selectedEnterprise");
        form.Add(new StringContent(context.SelectedFiscalYear.ToString(CultureInfo.InvariantCulture)), "selectedFiscalYear");
        return form;
    }

    private async Task<TResponse?> SendImportRequestAsync<TResponse>(QuickBooksImportRequestContext context, MultipartFormDataContent form, CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsync(context.RequestUri, form, cancellationToken).ConfigureAwait(false);
        return await HttpApiResponseHelper.ReadJsonResponseAsync<TResponse>(
            response,
            JsonOptions,
            "The QuickBooks import response was not valid JSON.",
            CreateFailureException(BuildOperationLabel(context.RequestUri)),
            cancellationToken).ConfigureAwait(false);
    }

    private static string BuildOperationLabel(string requestUri)
    {
        if (requestUri.EndsWith("/preview", StringComparison.OrdinalIgnoreCase))
        {
            return "QuickBooks preview";
        }

        if (requestUri.EndsWith("/commit", StringComparison.OrdinalIgnoreCase))
        {
            return "QuickBooks import commit";
        }

        return "QuickBooks import request";
    }

    private static int GetQuestionLength(QuickBooksImportGuidanceRequest request)
        => request.Question?.Length ?? 0;

    private static bool GetUsedFallback(QuickBooksImportGuidanceResponse? payload)
        => payload?.UsedFallback ?? false;

    private static string BuildFailureMessage(string operation, HttpStatusCode statusCode, string? responseBody)
    {
        var detail = HttpProblemDetailsParser.ExtractMessage(responseBody);
        return string.IsNullOrWhiteSpace(detail)
            ? $"{operation} failed with status {(int)statusCode}."
            : $"{operation} failed with status {(int)statusCode}: {detail}";
    }

    private static Func<HttpStatusCode, string?, Exception> CreateFailureException(string operation)
        => (statusCode, responseBody) => new InvalidOperationException(BuildFailureMessage(operation, statusCode, responseBody));

    private sealed record QuickBooksImportRequestContext(
        string RequestUri,
        byte[] FileBytes,
        string FileName,
        string SelectedEnterprise,
        int SelectedFiscalYear);
}
