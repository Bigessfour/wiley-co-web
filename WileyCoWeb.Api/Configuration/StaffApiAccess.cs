using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;

namespace WileyCoWeb.Api.Configuration;

/// <summary>
/// Finance API access for a Cognito principal. A token with no <c>cognito:groups</c> claim is allowed
/// so clerks are not locked out before groups are stamped. A token that lists groups and includes
/// neither Clerk nor admin is forbidden.
/// </summary>
public sealed class StaffApiAccessRequirement : IAuthorizationRequirement;

public sealed class StaffApiAccessHandler : AuthorizationHandler<StaffApiAccessRequirement>
{
    public const string ClerkGroup = "Clerk";

    public const string AdminGroup = "admin";

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        StaffApiAccessRequirement requirement)
    {
        if (HasApiAccess(context.User))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }

    public static bool HasApiAccess(ClaimsPrincipal user)
    {
        var groups = ReadGroups(user);
        if (groups.Count == 0)
        {
            return true;
        }

        return groups.Any(group =>
            string.Equals(group, ClerkGroup, StringComparison.OrdinalIgnoreCase)
            || string.Equals(group, AdminGroup, StringComparison.OrdinalIgnoreCase));
    }

    public static List<string> ReadGroups(ClaimsPrincipal user)
    {
        var groups = new List<string>();
        foreach (var claim in user.FindAll("cognito:groups"))
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
}
