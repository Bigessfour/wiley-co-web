using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
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

	public Task<QuickBooksImportPreviewResponse> PreviewAsync(byte[] fileBytes, string fileName, string selectedEnterprise, int selectedFiscalYear, CancellationToken cancellationToken = default)
		=> PostAsync<QuickBooksImportPreviewResponse>("api/imports/quickbooks/preview", fileBytes, fileName, selectedEnterprise, selectedFiscalYear, cancellationToken);

	public Task<QuickBooksImportCommitResponse> CommitAsync(byte[] fileBytes, string fileName, string selectedEnterprise, int selectedFiscalYear, CancellationToken cancellationToken = default)
		=> PostAsync<QuickBooksImportCommitResponse>("api/imports/quickbooks/commit", fileBytes, fileName, selectedEnterprise, selectedFiscalYear, cancellationToken);

	public async Task<QuickBooksImportGuidanceResponse> AskAsync(QuickBooksImportGuidanceRequest request, CancellationToken cancellationToken = default)
	{
		logger?.LogInformation("Requesting QuickBooks import guidance for question length {QuestionLength}", request.Question?.Length ?? 0);
		using var response = await httpClient.PostAsJsonAsync("api/imports/quickbooks/assistant", request, JsonOptions, cancellationToken).ConfigureAwait(false);

		if (!response.IsSuccessStatusCode)
		{
			var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
			logger?.LogWarning("QuickBooks import guidance request failed with status {StatusCode}", (int)response.StatusCode);
			throw new InvalidOperationException(BuildFailureMessage("QuickBooks import assistance", response.StatusCode, responseBody));
		}

		var payload = await response.Content.ReadFromJsonAsync<QuickBooksImportGuidanceResponse>(JsonOptions, cancellationToken).ConfigureAwait(false);
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

		using var response = await httpClient.PostAsync(requestUri, form, cancellationToken).ConfigureAwait(false);

		if (!response.IsSuccessStatusCode)
		{
			var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
			logger?.LogWarning("QuickBooks import request to {RequestUri} failed with status {StatusCode}", requestUri, (int)response.StatusCode);
			throw new InvalidOperationException(BuildFailureMessage(BuildOperationLabel(requestUri), response.StatusCode, responseBody));
		}

		var payload = await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions, cancellationToken).ConfigureAwait(false);
		logger?.LogInformation("QuickBooks import request to {RequestUri} completed successfully.", requestUri);
		return payload ?? throw new InvalidOperationException("The QuickBooks import response was empty.");
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

	private static string BuildFailureMessage(string operation, HttpStatusCode statusCode, string? responseBody)
	{
		var detail = ExtractFailureDetail(responseBody);
		return string.IsNullOrWhiteSpace(detail)
			? $"{operation} failed with status {(int)statusCode}."
			: $"{operation} failed with status {(int)statusCode}: {detail}";
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