namespace WileyCoWeb.Services.Auth;

public sealed class CognitoAuthOptions
{
    public const string SectionName = "Auth";

    public bool Enabled { get; set; }

    /// <summary>
    /// <see cref="CognitoSignInFlows.HostedUi"/> (default) uses Cognito Hosted UI + authorization code + PKCE.
    /// <see cref="CognitoSignInFlows.UserPassword"/> keeps the legacy direct <c>USER_PASSWORD_AUTH</c> API (removable).
    /// </summary>
    public string SignInFlow { get; set; } = CognitoSignInFlows.HostedUi;

    public CognitoClientOptions Cognito { get; set; } = new();

    public bool UsesHostedUi => Enabled && CognitoSignInFlows.IsHostedUi(SignInFlow);

    public bool UsesUserPassword => Enabled && CognitoSignInFlows.IsUserPassword(SignInFlow);
}

public sealed class CognitoClientOptions
{
    public string Region { get; set; } = "us-east-2";

    public string UserPoolId { get; set; } = string.Empty;

    public string ClientId { get; set; } = string.Empty;

    public string Authority { get; set; } = string.Empty;

    /// <summary>
    /// Cognito Hosted UI / managed login domain, e.g. <c>https://YOUR_PREFIX.auth.us-east-2.amazoncognito.com</c>.
    /// Used for global <c>/logout</c> so ClerkSuite and Trash Truck end the same browser session.
    /// </summary>
    public string OAuthDomain { get; set; } = string.Empty;

    /// <summary>Optional absolute override for OAuth redirect (otherwise derived from WASM base address).</summary>
    public string RedirectUri { get; set; } = string.Empty;

    /// <summary>Optional absolute override for post sign-out redirect (otherwise WASM base address + "/").</summary>
    public string PostLogoutRedirectUri { get; set; } = string.Empty;
}
