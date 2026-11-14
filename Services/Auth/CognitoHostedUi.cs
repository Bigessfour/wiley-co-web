namespace WileyCoWeb.Services.Auth;

/// <summary>
/// Hosted UI domain checks. The production file keeps a placeholder until Amplify
/// injects <c>COGNITO_OAUTH_DOMAIN</c>. Sign-out must not navigate to that placeholder.
/// </summary>
public static class CognitoHostedUi
{
    public const string PlaceholderMarker = "REPLACE_WITH_COGNITO_HOSTED_UI_PREFIX";

    public static bool IsConfiguredDomain(string? oauthDomain)
    {
        if (string.IsNullOrWhiteSpace(oauthDomain))
        {
            return false;
        }

        if (oauthDomain.Contains(PlaceholderMarker, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!Uri.TryCreate(oauthDomain.Trim(), UriKind.Absolute, out var uri))
        {
            return false;
        }

        return uri.Scheme == Uri.UriSchemeHttps
            && uri.Host.EndsWith(".amazoncognito.com", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Cognito global logout. Returns null when the domain is empty or still the placeholder,
    /// so callers fall back to local sign-out instead of a fake host.
    /// </summary>
    public static string? BuildGlobalLogoutUrl(string? oauthDomain, string? clientId, string postLogoutRedirectUri)
    {
        if (!IsConfiguredDomain(oauthDomain) || string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(postLogoutRedirectUri))
        {
            return null;
        }

        var domain = oauthDomain!.Trim().TrimEnd('/');
        return $"{domain}/logout?client_id={Uri.EscapeDataString(clientId.Trim())}&logout_uri={Uri.EscapeDataString(postLogoutRedirectUri)}";
    }
}
