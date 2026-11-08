using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace WileyCoWeb.Services.Auth;

public sealed class CognitoAuthenticationStateProvider : AuthenticationStateProvider
{
    private static readonly AuthenticationState AnonymousState =
        new(new ClaimsPrincipal(new ClaimsIdentity()));

    private readonly CognitoAuthService _authService;

    public CognitoAuthenticationStateProvider(CognitoAuthService authService)
    {
        _authService = authService;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        if (!_authService.IsEnabled)
        {
            return CreateDevelopmentState();
        }

        var session = await _authService.GetSessionAsync().ConfigureAwait(false);
        if (session is null || string.IsNullOrWhiteSpace(session.AccessToken))
        {
            return AnonymousState;
        }

        if (session.IsAccessTokenExpired(TimeSpan.Zero))
        {
            var token = await _authService.GetAccessTokenAsync().ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(token))
            {
                return AnonymousState;
            }

            session = await _authService.GetSessionAsync().ConfigureAwait(false);
            if (session is null)
            {
                return AnonymousState;
            }
        }

        return CreateAuthenticatedState(session.Username);
    }

    public void NotifyAuthenticationStateChanged()
        => NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());

    private static AuthenticationState CreateDevelopmentState()
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Name, "local-dev"),
            new Claim("auth_mode", "disabled")
        ], authenticationType: "Development");
        return new AuthenticationState(new ClaimsPrincipal(identity));
    }

    private static AuthenticationState CreateAuthenticatedState(string username)
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Name, username),
            new Claim("auth_mode", "cognito")
        ], authenticationType: "Cognito");
        return new AuthenticationState(new ClaimsPrincipal(identity));
    }
}
