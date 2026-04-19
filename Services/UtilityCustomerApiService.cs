using System.Net;
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

        var customers = await httpClient.GetJsonAsync<List<UtilityCustomerRecord>>(
            "api/utility-customers",
            JsonOptions,
            "The utility customer list response was not valid JSON.",
            (statusCode, responseBody) => CreateApiException(statusCode, responseBody, "list"),
            cancellationToken).ConfigureAwait(false);

        return customers ?? [];
    }

    public async Task<UtilityCustomerRecord> CreateCustomerAsync(UtilityCustomerUpsertRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        logger?.LogInformation("Creating utility customer record.");

        var customer = await httpClient.SendJsonAsync<UtilityCustomerRecord>(
            HttpMethod.Post,
            "api/utility-customers",
            request,
            JsonOptions,
            "The utility customer create response was not valid JSON.",
            (statusCode, responseBody) => CreateApiException(statusCode, responseBody, "create"),
            cancellationToken).ConfigureAwait(false);

        return customer ?? throw new InvalidOperationException("The utility customer create response was empty.");
    }

    public async Task<UtilityCustomerRecord> UpdateCustomerAsync(int customerId, UtilityCustomerUpsertRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(customerId);
        ArgumentNullException.ThrowIfNull(request);
        logger?.LogInformation("Updating utility customer {CustomerId}.", customerId);

        var customer = await httpClient.SendJsonAsync<UtilityCustomerRecord>(
            HttpMethod.Put,
            $"api/utility-customers/{customerId}",
            request,
            JsonOptions,
            "The utility customer update response was not valid JSON.",
            (statusCode, responseBody) => CreateApiException(statusCode, responseBody, "update"),
            cancellationToken).ConfigureAwait(false);

        return customer ?? throw new InvalidOperationException("The utility customer update response was empty.");
    }

    public async Task DeleteCustomerAsync(int customerId, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(customerId);
        logger?.LogInformation("Deleting utility customer {CustomerId}.", customerId);

        await httpClient.SendAsync(
            HttpMethod.Delete,
            $"api/utility-customers/{customerId}",
            (statusCode, responseBody) => CreateApiException(statusCode, responseBody, "delete"),
            cancellationToken).ConfigureAwait(false);
    }

    private static UtilityCustomerApiException CreateApiException(HttpStatusCode statusCode, string? responseBody, string operationName)
        => CreateApiExceptionCore(statusCode, responseBody, operationName);

    private static UtilityCustomerApiException CreateApiExceptionCore(HttpStatusCode statusCode, string? responseBody, string operationName)
    {
        var validationErrors = HttpProblemDetailsParser.ExtractValidationErrors(responseBody);

        if (validationErrors.Count > 0)
        {
            return CreateValidationApiException(statusCode, responseBody, operationName, validationErrors);
        }

        return CreateProblemApiException(statusCode, responseBody, operationName, validationErrors);
    }

    private static UtilityCustomerApiException CreateValidationApiException(
        HttpStatusCode statusCode,
        string? responseBody,
        string operationName,
        IReadOnlyDictionary<string, string[]> validationErrors)
    {
        var defaultMessage = $"The utility customer {operationName} request failed with status {(int)statusCode}.";
        var firstValidationMessage = validationErrors.Values.SelectMany(messages => messages).FirstOrDefault();
        var detail = HttpProblemDetailsParser.ExtractMessage(responseBody);
        var message = !string.IsNullOrWhiteSpace(firstValidationMessage)
            ? firstValidationMessage
            : detail ?? defaultMessage;

        return new UtilityCustomerApiException(message, statusCode, validationErrors);
    }

    private static UtilityCustomerApiException CreateProblemApiException(
        HttpStatusCode statusCode,
        string? responseBody,
        string operationName,
        IReadOnlyDictionary<string, string[]> validationErrors)
    {
        var defaultMessage = $"The utility customer {operationName} request failed with status {(int)statusCode}.";
        var problemMessage = HttpProblemDetailsParser.ExtractMessage(responseBody);

        return new UtilityCustomerApiException(
            string.IsNullOrWhiteSpace(problemMessage) ? defaultMessage : problemMessage,
            statusCode,
            validationErrors);
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
