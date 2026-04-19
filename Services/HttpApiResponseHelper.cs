using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace WileyCoWeb.Services;

internal static class HttpApiResponseHelper
{
    public static async Task<TResponse?> GetJsonAsync<TResponse>(
        this HttpClient httpClient,
        string requestUri,
        JsonSerializerOptions jsonOptions,
        string invalidJsonMessage,
        Func<HttpStatusCode, string?, Exception> createFailureException,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync(requestUri, cancellationToken).ConfigureAwait(false);
        return await ReadJsonResponseAsync<TResponse>(response, jsonOptions, invalidJsonMessage, createFailureException, cancellationToken).ConfigureAwait(false);
    }

    public static async Task<TResponse?> SendJsonAsync<TResponse>(
        this HttpClient httpClient,
        HttpMethod method,
        string requestUri,
        object requestBody,
        JsonSerializerOptions jsonOptions,
        string invalidJsonMessage,
        Func<HttpStatusCode, string?, Exception> createFailureException,
        CancellationToken cancellationToken = default)
    {
        using var content = JsonContent.Create(requestBody, options: jsonOptions);
        using var request = new HttpRequestMessage(method, requestUri)
        {
            Content = content
        };

        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        return await ReadJsonResponseAsync<TResponse>(response, jsonOptions, invalidJsonMessage, createFailureException, cancellationToken).ConfigureAwait(false);
    }

    public static async Task SendJsonAsync(
        this HttpClient httpClient,
        HttpMethod method,
        string requestUri,
        object requestBody,
        JsonSerializerOptions jsonOptions,
        Func<HttpStatusCode, string?, Exception> createFailureException,
        CancellationToken cancellationToken = default)
    {
        using var content = JsonContent.Create(requestBody, options: jsonOptions);
        using var request = new HttpRequestMessage(method, requestUri)
        {
            Content = content
        };

        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw await CreateFailureExceptionAsync(response, createFailureException, cancellationToken).ConfigureAwait(false);
        }
    }

    public static async Task SendAsync(
        this HttpClient httpClient,
        HttpMethod method,
        string requestUri,
        Func<HttpStatusCode, string?, Exception> createFailureException,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(method, requestUri);
        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw await CreateFailureExceptionAsync(response, createFailureException, cancellationToken).ConfigureAwait(false);
        }
    }

    public static async Task<TResponse?> ReadJsonResponseAsync<TResponse>(
        HttpResponseMessage response,
        JsonSerializerOptions jsonOptions,
        string invalidJsonMessage,
        Func<HttpStatusCode, string?, Exception> createFailureException,
        CancellationToken cancellationToken = default)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw await CreateFailureExceptionAsync(response, createFailureException, cancellationToken).ConfigureAwait(false);
        }

        try
        {
            return await response.Content.ReadFromJsonAsync<TResponse>(jsonOptions, cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(invalidJsonMessage, ex);
        }
    }

    private static async Task<Exception> CreateFailureExceptionAsync(
        HttpResponseMessage response,
        Func<HttpStatusCode, string?, Exception> createFailureException,
        CancellationToken cancellationToken)
    {
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return createFailureException(response.StatusCode, responseBody);
    }
}