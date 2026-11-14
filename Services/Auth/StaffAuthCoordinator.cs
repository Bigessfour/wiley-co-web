using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.Extensions.Options;

namespace WileyCoWeb.Services.Auth;

public sealed class StaffAuthCoordinator
{
    private readonly CognitoAuthOptions _options;
    private readonly CognitoAuthService _passwordAuth;
    private readonly NavigationManager _navigation;

    public StaffAuthCoordinator(
        IOptions<CognitoAuthOptions> options,
        CognitoAuthService passwordAuth,
        NavigationManager navigation)
    {
        _options = options.Value;
        _passwordAuth = passwordAuth;
        _navigation = navigation;
    }

    public bool IsEnabled => _options.Enabled
        && !string.IsNullOrWhiteSpace(_options.Cognito.ClientId);

    public bool UsesHostedUi => _options.UsesHostedUi;

    public bool UsesUserPassword => _options.UsesUserPassword;

    public Task SignOutAsync()
    {
        if (!IsEnabled)
        {
            return Task.CompletedTask;
        }

        if (UsesHostedUi)
        {
            var logoutUrl = CognitoHostedUi.BuildGlobalLogoutUrl(
                _options.Cognito.OAuthDomain,
                _options.Cognito.ClientId,
                ResolvePostLogoutRedirectUri());
            if (logoutUrl is not null)
            {
                _navigation.NavigateTo(logoutUrl, forceLoad: true);
                return Task.CompletedTask;
            }

            _navigation.NavigateToLogout("authentication/logout");
            return Task.CompletedTask;
        }

        return _passwordAuth.SignOutAsync();
    }

    public void NavigateToLogin(string? returnUrl)
    {
        if (!IsEnabled)
        {
            return;
        }

        if (UsesHostedUi)
        {
            _navigation.NavigateToLogin(
                returnUrl is null ? "authentication/login" : $"authentication/login?returnUrl={Uri.EscapeDataString(returnUrl)}");
            return;
        }

        var loginPath = string.IsNullOrWhiteSpace(returnUrl)
            ? "/authentication/legacy-login"
            : $"/authentication/legacy-login?returnUrl={Uri.EscapeDataString(returnUrl)}";
        _navigation.NavigateTo(loginPath);
    }

    private string ResolvePostLogoutRedirectUri()
    {
        if (!string.IsNullOrWhiteSpace(_options.Cognito.PostLogoutRedirectUri))
        {
            return _options.Cognito.PostLogoutRedirectUri.Trim();
        }

        return _navigation.ToAbsoluteUri("/").ToString();
    }
}
