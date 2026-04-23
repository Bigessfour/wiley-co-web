using System.Text.Json;

namespace WileyCoWeb.Services;

internal static class HttpProblemDetailsParser
{
    private static readonly JsonSerializerOptions ValidationOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly IReadOnlyDictionary<string, string[]> EmptyValidationErrors = new Dictionary<string, string[]>();

    public static string? ExtractMessage(string? responseBody)
    {
        if (responseBody is null)
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(responseBody);
            var root = document.RootElement;

            if (root.ValueKind == JsonValueKind.String)
            {
                return root.GetString()?.Trim();
            }

            var message = ExtractObjectMessage(root);
            return string.IsNullOrWhiteSpace(message) ? responseBody.Trim() : message;
        }
        catch (JsonException)
        {
            return responseBody.Trim();
        }
    }

    public static IReadOnlyDictionary<string, string[]> ExtractValidationErrors(string? responseBody)
    {
        if (responseBody is null)
        {
            return EmptyValidationErrors;
        }

        try
        {
            using var document = JsonDocument.Parse(responseBody);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("errors", out var errorsElement)
                || errorsElement.ValueKind != JsonValueKind.Object)
            {
                return EmptyValidationErrors;
            }

            return JsonSerializer.Deserialize<Dictionary<string, string[]>>(errorsElement.GetRawText(), ValidationOptions)
                   ?? EmptyValidationErrors;
        }
        catch (JsonException)
        {
            return EmptyValidationErrors;
        }
    }

    private static string? ExtractObjectMessage(JsonElement root)
        => root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty("detail", out var detailElement)
            && detailElement.ValueKind == JsonValueKind.String
                ? detailElement.GetString()?.Trim()
                : null;
}
