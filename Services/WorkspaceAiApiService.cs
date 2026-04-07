using System.Net.Http.Json;
using System.Text.Json;
using WileyCoWeb.Contracts;

namespace WileyCoWeb.Services;

public sealed class WorkspaceAiApiService
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
	{
		PropertyNameCaseInsensitive = true
	};

	private readonly HttpClient httpClient;

	public WorkspaceAiApiService(HttpClient httpClient)
	{
		this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
	}

	public async Task<WorkspaceChatResponse> AskAsync(WorkspaceChatRequest request, CancellationToken cancellationToken = default)
	{
		var response = await httpClient.PostAsJsonAsync("api/ai/chat", request, JsonOptions, cancellationToken).ConfigureAwait(false);
		var payload = await response.Content.ReadFromJsonAsync<WorkspaceChatResponse>(JsonOptions, cancellationToken).ConfigureAwait(false);

		if (!response.IsSuccessStatusCode)
		{
			var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
			throw new InvalidOperationException(string.IsNullOrWhiteSpace(responseBody)
				? $"Workspace AI request failed with status {(int)response.StatusCode}."
				: responseBody);
		}

		return payload ?? throw new InvalidOperationException("The workspace AI response was empty.");
	}
}
