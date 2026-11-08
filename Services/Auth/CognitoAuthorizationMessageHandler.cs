using System.Net.Http.Headers;

namespace WileyCoWeb.Services.Auth;

/// <summary>
/// Attaches Cognito access tokens to workspace API calls when staff auth is enabled.
/// </summary>
public sealed class CognitoAuthorizationMessageHandler : DelegatingHandler
{
    private readonly CognitoAuthService _authService;

    public CognitoAuthorizationMessageHandler(CognitoAuthService authService)
    {
        _authService = authService;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (_authService.IsEnabled)
        {
            var accessToken = await _authService.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(accessToken))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            }
        }

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
