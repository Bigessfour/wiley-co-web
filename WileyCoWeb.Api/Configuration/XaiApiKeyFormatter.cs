using System.Text.Json;

namespace WileyCoWeb.Api.Configuration;

/// <summary>
/// Extracts a usable xAI API key from Secrets Manager or environment values.
/// App Runner maps secrets into <c>XAI_API_KEY</c> as the raw SecretString, which is often JSON.
/// </summary>
public static class XaiApiKeyFormatter
{
    private static readonly string[] JsonPropertyNames =
    [
        "XAI_API_KEY",
        "ApiKey",
        "XaiApiKey",
        "GrokApiKey",
        "XAI:ApiKey",
        "secret"
    ];

    /// <summary>
    /// Returns a trimmed bare API key, or extracts one from a JSON secret envelope.
    /// Returns <c>null</c> when input is empty/whitespace, JSON is invalid, or no supported property is present.
    /// </summary>
    public static string? ExtractUsableKey(string? secretValue)
    {
        if (string.IsNullOrWhiteSpace(secretValue))
        {
            return null;
        }

        var trimmed = secretValue.Trim();
        if (!trimmed.StartsWith("{", StringComparison.Ordinal))
        {
            return trimmed;
        }

        try
        {
            using var document = JsonDocument.Parse(trimmed);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            foreach (var propertyName in JsonPropertyNames)
            {
                if (document.RootElement.TryGetProperty(propertyName, out var element)
                    && element.ValueKind == JsonValueKind.String)
                {
                    var value = element.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return value.Trim();
                    }
                }
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }
}
