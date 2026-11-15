using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;

namespace WileyCoWeb.Api.Configuration;

/// <summary>
/// Finance API access for a Cognito principal. Groups come from <c>cognito:groups</c> and from
/// <c>ClaimTypes.Role</c>, which Hosted UI uses for the same claim. A token with no groups is allowed
/// so staff are not locked out before the group is stamped. A token that lists groups and does not
/// include Staff is forbidden.
/// </summary>
public sealed class StaffApiAccessRequirement : IAuthorizationRequirement;

public sealed class StaffApiAccessHandler : AuthorizationHandler<StaffApiAccessRequirement>
{
    public const string StaffGroup = "Staff";

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
            string.Equals(group, StaffGroup, StringComparison.OrdinalIgnoreCase));
    }

    public static List<string> ReadGroups(ClaimsPrincipal user)
    {
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
