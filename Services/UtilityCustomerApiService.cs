using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using WileyCoWeb.Contracts;

namespace WileyCoWeb.Services;

public sealed class UtilityCustomerApiService(HttpClient httpClient, ILogger<UtilityCustomerApiService>? logger = null)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    public async Task<IReadOnlyList<UtilityCustomerRecord>> GetCustomersAsync(CancellationToken cancellationToken = default)
    {
        logger?.LogInformation("Requesting utility customers from api/utility-customers");

        try
        {
            var customers = await httpClient.GetFromJsonAsync<List<UtilityCustomerRecord>>("api/utility-customers", JsonOptions, cancellationToken);
            return customers ?? [];
        }
        catch (JsonException ex)
        {
            logger?.LogWarning(ex, "Utility customer list response was not valid JSON.");
            throw new InvalidOperationException("The utility customer list response was not valid JSON.", ex);
        }
    }

    public async Task<UtilityCustomerRecord> CreateCustomerAsync(UtilityCustomerUpsertRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var response = await httpClient.PostAsJsonAsync("api/utility-customers", request, JsonOptions, cancellationToken);
        return await ReadCustomerResponseAsync(response, "create", cancellationToken);
    }

    public async Task<UtilityCustomerRecord> UpdateCustomerAsync(int customerId, UtilityCustomerUpsertRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(customerId);
        ArgumentNullException.ThrowIfNull(request);

        using var response = await httpClient.PutAsJsonAsync($"api/utility-customers/{customerId}", request, JsonOptions, cancellationToken);
        return await ReadCustomerResponseAsync(response, "update", cancellationToken);
    }

    public async Task DeleteCustomerAsync(int customerId, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(customerId);

        using var response = await httpClient.DeleteAsync($"api/utility-customers/{customerId}", cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        throw await CreateApiExceptionAsync(response, "delete", cancellationToken);
    }

    private async Task<UtilityCustomerRecord> ReadCustomerResponseAsync(HttpResponseMessage response, string operationName, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw await CreateApiExceptionAsync(response, operationName, cancellationToken);
        }

        try
        {
            var customer = await response.Content.ReadFromJsonAsync<UtilityCustomerRecord>(JsonOptions, cancellationToken);
            return customer ?? throw new InvalidOperationException($"The utility customer {operationName} response was empty.");
        }
        catch (JsonException ex)
        {
            logger?.LogWarning(ex, "Utility customer {OperationName} response was not valid JSON.", operationName);
            throw new InvalidOperationException($"The utility customer {operationName} response was not valid JSON.", ex);
        }
    }

    private static async Task<UtilityCustomerApiException> CreateApiExceptionAsync(HttpResponseMessage response, string operationName, CancellationToken cancellationToken)
    {
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        var (message, validationErrors) = ParseError(response.StatusCode, responseBody, operationName);
        return new UtilityCustomerApiException(message, response.StatusCode, validationErrors);
    }

    private static (string Message, IReadOnlyDictionary<string, string[]> ValidationErrors) ParseError(HttpStatusCode statusCode, string? responseBody, string operationName)
    {
        var validationErrors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        var defaultMessage = $"The utility customer {operationName} request failed with status {(int)statusCode}.";

        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return (defaultMessage, validationErrors);
        }

        return TryParseJsonError(responseBody, defaultMessage, validationErrors);
    }

    private static (string Message, IReadOnlyDictionary<string, string[]> ValidationErrors) TryParseJsonError(string responseBody, string defaultMessage, Dictionary<string, string[]> validationErrors)
    {
        try
        {
            using var document = JsonDocument.Parse(responseBody);
            var root = document.RootElement;

            if (root.ValueKind == JsonValueKind.String)
            {
                var stringMessage = root.GetString();
                return (string.IsNullOrWhiteSpace(stringMessage) ? defaultMessage : stringMessage.Trim(), validationErrors);
            }

            if (root.ValueKind == JsonValueKind.Object)
            {
                return ParseObjectError(root, defaultMessage, validationErrors);
            }
        }
        catch (JsonException)
        {
            // Fall back to plain-text normalization below.
        }

        return ParsePlainTextError(responseBody, defaultMessage);
    }

    private static (string Message, IReadOnlyDictionary<string, string[]> ValidationErrors) ParseObjectError(JsonElement root, string defaultMessage, Dictionary<string, string[]> validationErrors)
    {
        ExtractValidationErrors(root, validationErrors);

        var title = GetStringProperty(root, "title");
        var detail = GetStringProperty(root, "detail");

        if (validationErrors.Count > 0)
        {
            var firstValidationMessage = validationErrors.Values.SelectMany(messages => messages).FirstOrDefault();
            return (!string.IsNullOrWhiteSpace(firstValidationMessage)
                    ? firstValidationMessage
                    : title ?? detail ?? defaultMessage,
                validationErrors);
        }

        var problemMessage = detail ?? title;
        if (!string.IsNullOrWhiteSpace(problemMessage))
        {
            return (problemMessage, validationErrors);
        }

        return (defaultMessage, validationErrors);
    }

    private static void ExtractValidationErrors(JsonElement root, Dictionary<string, string[]> validationErrors)
    {
        if (root.TryGetProperty("errors", out var errorsElement) && errorsElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in errorsElement.EnumerateObject())
            {
                var messages = property.Value.ValueKind == JsonValueKind.Array
                    ? property.Value.EnumerateArray()
                        .Where(item => item.ValueKind == JsonValueKind.String)
                        .Select(item => item.GetString())
                        .Where(item => !string.IsNullOrWhiteSpace(item))
                        .Select(item => item!.Trim())
                        .ToArray()
                    : [];

                if (messages.Length > 0)
                {
                    validationErrors[property.Name] = messages;
                }
            }
        }
    }

    private static string? GetStringProperty(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var propertyElement) && propertyElement.ValueKind == JsonValueKind.String
            ? propertyElement.GetString()?.Trim()
            : null;
    }

    private static (string Message, IReadOnlyDictionary<string, string[]> ValidationErrors) ParsePlainTextError(string responseBody, string defaultMessage)
    {
        var normalizedBody = responseBody.Trim();
        if (normalizedBody.Length >= 2 && normalizedBody.StartsWith('"') && normalizedBody.EndsWith('"'))
        {
            normalizedBody = normalizedBody[1..^1];
        }

        return (string.IsNullOrWhiteSpace(normalizedBody) ? defaultMessage : normalizedBody, new Dictionary<string, string[]>());
    }
}

public sealed class UtilityCustomerApiException : InvalidOperationException
{
    private static readonly IReadOnlyDictionary<string, string[]> EmptyValidationErrors = new Dictionary<string, string[]>();

    public UtilityCustomerApiException()
    {
        ValidationErrors = EmptyValidationErrors;
    }

    public UtilityCustomerApiException(string message)
        : base(message)
    {
        ValidationErrors = EmptyValidationErrors;
    }

    public UtilityCustomerApiException(string message, Exception innerException)
        : base(message, innerException)
    {
        ValidationErrors = EmptyValidationErrors;
    }

    public UtilityCustomerApiException(string message, HttpStatusCode statusCode, IReadOnlyDictionary<string, string[]> validationErrors)
        : base(message)
    {
        StatusCode = statusCode;
        ValidationErrors = validationErrors;
    }

    public HttpStatusCode StatusCode { get; }

    public IReadOnlyDictionary<string, string[]> ValidationErrors { get; }
}