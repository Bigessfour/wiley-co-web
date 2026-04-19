using System.Text.Json;

namespace WileyCoWeb.Services;

internal static class HttpProblemDetailsParser
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly IReadOnlyDictionary<string, string[]> EmptyValidationErrors = new Dictionary<string, string[]>();

    public static string? ExtractMessage(string? responseBody)
        => ExtractMessageCore(responseBody);

    public static IReadOnlyDictionary<string, string[]> ExtractValidationErrors(string? responseBody)
        => ExtractValidationErrorsCore(responseBody);

    private static string? ExtractMessageCore(string? responseBody)
    {
        return string.IsNullOrWhiteSpace(responseBody)
            ? null
            : TryGetProblemDetailsPayload(responseBody, out var payload)
                ? ExtractPayloadMessage(payload)
                : responseBody.Trim();
    }

    private static IReadOnlyDictionary<string, string[]> ExtractValidationErrorsCore(string? responseBody)
    {
        if (!TryGetProblemDetailsPayload(responseBody, out var payload) || payload is null)
        {
            return EmptyValidationErrors;
        }

        return GetValidationErrors(payload);
    }

    private static bool TryGetProblemDetailsPayload(string? responseBody, out ProblemDetailsPayload? payload)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            payload = null;
            return false;
        }

        try
        {
            payload = JsonSerializer.Deserialize<ProblemDetailsPayload>(responseBody, JsonOptions);
            return true;
        }
        catch (JsonException)
        {
            payload = null;
            return false;
        }
    }

    private static IReadOnlyDictionary<string, string[]> GetValidationErrors(ProblemDetailsPayload payload)
        => payload.Errors ?? EmptyValidationErrors;

    private static string? ExtractPayloadMessage(ProblemDetailsPayload? payload)
    {
        if (payload is null)
        {
            return null;
        }

        return GetPayloadMessage(payload);
    }

    private static string? GetPayloadMessage(ProblemDetailsPayload payload)
    {
        var detail = NormalizePayloadValue(payload.Detail);
        if (detail is not null)
        {
            return detail;
        }

        return NormalizePayloadValue(payload.Title);
    }

    private static string? NormalizePayloadValue(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record ProblemDetailsPayload(
        Dictionary<string, string[]>? Errors,
        string? Detail,
        string? Title);
}
