namespace WileyCoWeb.Services.Auth;

public sealed class CognitoAuthSession
{
    public string AccessToken { get; init; } = string.Empty;

    public string IdToken { get; init; } = string.Empty;

    public string RefreshToken { get; init; } = string.Empty;

    public DateTimeOffset AccessTokenExpiresAtUtc { get; init; }

    public string Username { get; init; } = string.Empty;

    public bool IsAccessTokenExpired(TimeSpan skew)
        => DateTimeOffset.UtcNow >= AccessTokenExpiresAtUtc - skew;
}
