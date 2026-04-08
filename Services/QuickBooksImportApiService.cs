using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using WileyCoWeb.Contracts;

namespace WileyCoWeb.Services;

public sealed class QuickBooksImportApiService
{
	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNameCaseInsensitive = true
	};

	private readonly HttpClient httpClient;
	private readonly ILogger<QuickBooksImportApiService>? logger;

	public QuickBooksImportApiService(HttpClient httpClient, ILogger<QuickBooksImportApiService>? logger = null)
	{
		this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
		this.logger = logger;
	}

	public Task<QuickBooksImportPreviewResponse> PreviewAsync(byte[] fileBytes, string fileName, string selectedEnterprise, int selectedFiscalYear, CancellationToken cancellationToken = default)
		=> PostAsync<QuickBooksImportPreviewResponse>("api/imports/quickbooks/preview", fileBytes, fileName, selectedEnterprise, selectedFiscalYear, cancellationToken);

	public Task<QuickBooksImportCommitResponse> CommitAsync(byte[] fileBytes, string fileName, string selectedEnterprise, int selectedFiscalYear, CancellationToken cancellationToken = default)
		=> PostAsync<QuickBooksImportCommitResponse>("api/imports/quickbooks/commit", fileBytes, fileName, selectedEnterprise, selectedFiscalYear, cancellationToken);

    public async Task<QuickBooksImportGuidanceResponse> AskAsync(QuickBooksImportGuidanceRequest request, CancellationToken cancellationToken = default)
    {
        logger?.LogInformation("Requesting QuickBooks import guidance for question length {QuestionLength}", request.Question?.Length ?? 0);
		var response = await httpClient.PostAsJsonAsync("api/imports/quickbooks/assistant", request, JsonOptions, cancellationToken).ConfigureAwait(false);
		var payload = await response.Content.ReadFromJsonAsync<QuickBooksImportGuidanceResponse>(JsonOptions, cancellationToken).ConfigureAwait(false);

		if (!response.IsSuccessStatusCode)
		{
			var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
			logger?.LogWarning("QuickBooks import guidance request failed with status {StatusCode}", (int)response.StatusCode);
			throw new InvalidOperationException(string.IsNullOrWhiteSpace(responseBody)
				? $"Import assistance failed with status {(int)response.StatusCode}."
				: responseBody);
		}

		logger?.LogInformation("QuickBooks import guidance request completed successfully (usedFallback={UsedFallback}).", payload?.UsedFallback ?? false);
		return payload ?? throw new InvalidOperationException("The QuickBooks import guidance response was empty.");
    }

	private async Task<TResponse> PostAsync<TResponse>(string requestUri, byte[] fileBytes, string fileName, string selectedEnterprise, int selectedFiscalYear, CancellationToken cancellationToken)
	{
		logger?.LogInformation("Posting QuickBooks import request to {RequestUri} for file {FileName} ({ByteCount} bytes) in {Enterprise} FY {FiscalYear}", requestUri, Path.GetFileName(fileName), fileBytes.LongLength, selectedEnterprise, selectedFiscalYear);
		using var form = new MultipartFormDataContent();
		using var fileContent = new ByteArrayContent(fileBytes);
		fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

		form.Add(fileContent, "file", fileName);
		form.Add(new StringContent(selectedEnterprise), "selectedEnterprise");
		form.Add(new StringContent(selectedFiscalYear.ToString(CultureInfo.InvariantCulture)), "selectedFiscalYear");

		var response = await httpClient.PostAsync(requestUri, form, cancellationToken).ConfigureAwait(false);
		var payload = await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions, cancellationToken).ConfigureAwait(false);

		if (!response.IsSuccessStatusCode)
		{
			var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
			logger?.LogWarning("QuickBooks import request to {RequestUri} failed with status {StatusCode}", requestUri, (int)response.StatusCode);
			throw new InvalidOperationException(string.IsNullOrWhiteSpace(responseBody)
				? $"Import request failed with status {(int)response.StatusCode}."
				: responseBody);
		}

		logger?.LogInformation("QuickBooks import request to {RequestUri} completed successfully.", requestUri);
		return payload ?? throw new InvalidOperationException("The QuickBooks import response was empty.");
	}
}