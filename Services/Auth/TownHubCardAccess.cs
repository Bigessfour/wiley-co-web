using System.Net;

namespace WileyCoWeb.Services.Auth;

/// <summary>
/// Which Town Hub cards a signed-in group may see, and which links are not the shared login.
/// </summary>
public static class TownHubCardAccess
{
    public const string OfficeNetworkNote =
        "Office network only. Sign in again in that app. Town Hub sign-in does not carry over.";

    public const string SharedSignInNote = "Uses the same Town Hub sign-in.";

    public const string ComingSoonLabel = "Coming soon";

    public static bool IsVisible(
        string appId,
        IReadOnlyList<string>? allowedGroups,
        IReadOnlyList<string> userGroups,
        bool authEnabled)
    {
        if (!authEnabled)
        {
            return true;
        }

        var allowed = allowedGroups is { Count: > 0 }
            ? allowedGroups
            : DefaultGroups(appId);

        if (userGroups.Any(group => string.Equals(group, CognitoGroupsHelper.AdminGroup, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (userGroups.Count == 0)
        {
            return allowed.Any(group => string.Equals(group, CognitoGroupsHelper.ClerkGroup, StringComparison.OrdinalIgnoreCase));
        }

        return userGroups.Any(userGroup =>
            allowed.Any(group => string.Equals(group, userGroup, StringComparison.OrdinalIgnoreCase)));
    }

    public static IReadOnlyList<string> DefaultGroups(string appId) =>
        string.Equals(appId, "04", StringComparison.Ordinal)
            ? [CognitoGroupsHelper.ClerkGroup, CognitoGroupsHelper.DriverGroup, CognitoGroupsHelper.AdminGroup]
            : [CognitoGroupsHelper.ClerkGroup, CognitoGroupsHelper.AdminGroup];

    /// <summary>
    /// Absolute http links that are not loopback. Those apps are on the office tailnet and do not share this login.
    /// </summary>
    public static bool IsOfficeNetworkOnly(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)
            || !Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || !uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || uri.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)
            || uri.Host.Equals("[::1]", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (IPAddress.TryParse(uri.Host, out var address) && IPAddress.IsLoopback(address))
        {
            return false;
        }

        return true;
    }
}
