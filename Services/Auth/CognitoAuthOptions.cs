namespace WileyCoWeb.Services.Auth;

public sealed class CognitoAuthOptions
{
    public const string SectionName = "Auth";

    public bool Enabled { get; set; }

    public CognitoClientOptions Cognito { get; set; } = new();
}

public sealed class CognitoClientOptions
{
    public string Region { get; set; } = "us-east-2";

    public string UserPoolId { get; set; } = string.Empty;

    public string ClientId { get; set; } = string.Empty;

    public string Authority { get; set; } = string.Empty;
}
