using System.Security.Claims;
using System.Text.Json;

namespace WileyCoWeb.Services.Auth;

public static class CognitoGroupsHelper
{
    public const string DriverGroup = "Driver";

    public const string ClerkGroup = "Clerk";

    public const string AdminGroup = "admin";

    public static IReadOnlyList<string> GetGroups(ClaimsPrincipal? user)
    {
        if (user?.Identity?.IsAuthenticated != true)
        {
            return Array.Empty<string>();
        }

        var groups = new List<string>();
        foreach (var claim in user.FindAll("cognito:groups"))
        {
            if (!string.IsNullOrWhiteSpace(claim.Value))
            {
                groups.Add(claim.Value.Trim());
            }
        }

        if (groups.Count == 0)
        {
            var arrayClaim = user.FindFirst("cognito:groups");
            if (arrayClaim is not null && arrayClaim.Value.StartsWith("[", StringComparison.Ordinal))
            {
                try
                {
                    var parsed = JsonSerializer.Deserialize<string[]>(arrayClaim.Value);
                    if (parsed is { Length: > 0 })
                    {
                        groups.AddRange(parsed.Where(static g => !string.IsNullOrWhiteSpace(g)).Select(static g => g.Trim()));
                    }
                }
                catch (JsonException)
                {
                    // Fall through — Trash Truck applies the same group rules after Hosted UI sign-in.
                }
            }
        }

        return groups;
    }

    /// <summary>Trash Truck launch segment: <c>/driver</c> or <c>/clerk</c> (default clerk when groups are unknown).</summary>
    public static string GetTrashTruckPathSegment(IReadOnlyList<string> groups)
    {
        if (groups.Any(static g => string.Equals(g, DriverGroup, StringComparison.OrdinalIgnoreCase))
            && !groups.Any(static g => string.Equals(g, ClerkGroup, StringComparison.OrdinalIgnoreCase)
                || string.Equals(g, AdminGroup, StringComparison.OrdinalIgnoreCase)))
        {
            return "driver";
        }

        return "clerk";
    }

    public static string BuildTrashTruckHubUrl(string baseUrl, IReadOnlyList<string> groups)
    {
        var trimmed = baseUrl.Trim().TrimEnd('/');
        var segment = GetTrashTruckPathSegment(groups);
        return $"{trimmed}/{segment}?from=town-hub";
    }
}
