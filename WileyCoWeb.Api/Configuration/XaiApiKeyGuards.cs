using System.Security.Cryptography;
using System.Text;

namespace WileyCoWeb.Api.Configuration;

/// <summary>
/// Runtime safety checks for xAI credentials (no logging of raw secrets).
/// </summary>
public static class XaiApiKeyGuards
{
    public const int MinimumKeyLength = 12;

    private static readonly string[] PlaceholderSubstrings =
    [
        "your_xai",
        "your-xai",
        "changeme",
        "change_me",
        "example",
        "placeholder",
        "replace_me",
        "insert",
        "xxxxx",
        "todo"
    ];

    /// <summary>
    /// When non-null, the key should not be used; message is safe to log (no secret material).
    /// </summary>
    public static string? TryGetRuntimeValidationIssue(string? apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return null;
        }

        var key = apiKey.Trim();

        if (key.StartsWith("{", StringComparison.Ordinal))
        {
            return "Key still looks like JSON after normalization; expected a bare xAI API key or resolvable JSON envelope.";
        }

        if (key.Length < MinimumKeyLength)
        {
            return $"Key length {key.Length} is below minimum {MinimumKeyLength}.";
        }

        var lowered = key.ToLowerInvariant();
        foreach (var phrase in PlaceholderSubstrings)
        {
            if (lowered.Contains(phrase, StringComparison.Ordinal))
            {
                return "Key matches a placeholder pattern and must be replaced with a real xAI credential.";
            }
        }

        return null;
    }

    /// <summary>
    /// SHA-256 prefix (hex) for correlating deployments without exposing the key.
    /// </summary>
    public static string ComputeFingerprint(string? apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return "(empty)";
        }

        var trimmed = apiKey.Trim();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(trimmed));
        return Convert.ToHexString(hash.AsSpan(0, 8));
    }

    /// <summary>
    /// Safe preview: length + fingerprint only.
    /// </summary>
    public static string DescribeForLog(string? apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return "length=0 fingerprint=(empty)";
        }

        var trimmed = apiKey.Trim();
        return $"length={trimmed.Length} fingerprint={ComputeFingerprint(trimmed)}";
    }
}
