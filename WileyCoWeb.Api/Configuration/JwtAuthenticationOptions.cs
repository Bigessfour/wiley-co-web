namespace WileyCoWeb.Api.Configuration;

/// <summary>
/// Config-driven JWT authentication scaffolding for Cognito / Amplify Auth.
/// Disabled by default until town IdP values are configured in App Runner.
/// </summary>
public sealed class JwtAuthenticationOptions
{
    public const string SectionName = "Authentication:Jwt";

    public bool Enabled { get; set; }

    /// <summary>Cognito issuer URL, e.g. https://cognito-idp.us-east-2.amazonaws.com/us-east-2_xxxxx</summary>
    public string Authority { get; set; } = string.Empty;

    /// <summary>App client audience / resource identifier.</summary>
    /// <summary>
    /// App client id (or resource server identifier). Validates <c>aud</c> on access tokens and
    /// falls back to <c>client_id</c> for Cognito access tokens that omit <c>aud</c>.
    /// Clerks should send the <strong>access token</strong> (not the ID token) on API calls.
    /// </summary>
    public string Audience { get; set; } = "wiley-widget-api";

    public bool RequireHttpsMetadata { get; set; } = true;
}
