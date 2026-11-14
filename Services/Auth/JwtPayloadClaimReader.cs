using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace WileyCoWeb.Services.Auth;

/// <summary>Best-effort JWT payload claim extraction for UI routing (not for security decisions).</summary>
internal static class JwtPayloadClaimReader
{
    public static IEnumerable<Claim> ReadClaims(string? jwt)
    {
        if (string.IsNullOrWhiteSpace(jwt))
        {
            yield break;
        }

        var parts = jwt.Split('.');
        if (parts.Length < 2)
        {
            yield break;
        }

        var payloadJson = DecodeBase64Url(parts[1]);
        if (payloadJson is null)
        {
            yield break;
        }

        using var document = JsonDocument.Parse(payloadJson);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            yield break;
        }

        foreach (var property in document.RootElement.EnumerateObject())
        {
            switch (property.Value.ValueKind)
            {
                case JsonValueKind.String:
                    yield return new Claim(property.Name, property.Value.GetString() ?? string.Empty);
                    break;
                case JsonValueKind.Array when property.Name == "cognito:groups":
                    foreach (var element in property.Value.EnumerateArray())
                    {
                        if (element.ValueKind == JsonValueKind.String
                            && !string.IsNullOrWhiteSpace(element.GetString()))
                        {
                            yield return new Claim("cognito:groups", element.GetString()!);
                        }
                    }

                    break;
            }
        }
    }

    private static string? DecodeBase64Url(string segment)
    {
        try
        {
            var padded = segment.Replace('-', '+').Replace('_', '/');
            switch (padded.Length % 4)
            {
                case 2:
                    padded += "==";
                    break;
                case 3:
                    padded += "=";
                    break;
            }

            var bytes = Convert.FromBase64String(padded);
            return Encoding.UTF8.GetString(bytes);
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
