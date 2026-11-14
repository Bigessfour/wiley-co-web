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
            AddGroupValues(groups, claim.Value);
        }

        // Hosted UI maps RoleClaim "cognito:groups" onto ClaimTypes.Role.
        foreach (var claim in user.FindAll(ClaimTypes.Role))
        {
            AddGroupValues(groups, claim.Value);
        }

        return groups;
    }

    private static void AddGroupValues(List<string> groups, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var trimmed = value.Trim();
        if (trimmed.StartsWith('['))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<string[]>(trimmed);
                if (parsed is { Length: > 0 })
                {
                    foreach (var group in parsed)
                    {
                        AddDistinct(groups, group);
                    }

                    return;
                }
            }
            catch (JsonException)
            {
                // Keep the raw value when it is not a JSON string array.
            }
        }

        AddDistinct(groups, trimmed);
    }

    private static void AddDistinct(List<string> groups, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var trimmed = value.Trim();
        if (groups.Any(group => string.Equals(group, trimmed, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        groups.Add(trimmed);
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
