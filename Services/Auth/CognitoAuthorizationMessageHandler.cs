using System.Net.Http.Headers;

namespace WileyCoWeb.Services.Auth;

/// <summary>
/// Attaches Cognito access tokens to workspace API calls when staff auth is enabled.
/// </summary>
public sealed class CognitoAuthorizationMessageHandler : DelegatingHandler
{
    private readonly CognitoAuthOptions _options;
    private readonly IStaffAccessTokenProvider _accessTokenProvider;

    public CognitoAuthorizationMessageHandler(
        Microsoft.Extensions.Options.IOptions<CognitoAuthOptions> options,
        IStaffAccessTokenProvider accessTokenProvider)
    {
        _options = options.Value;
        _accessTokenProvider = accessTokenProvider;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (_options.Enabled)
        {
            var accessToken = await _accessTokenProvider.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(accessToken))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            }
        }

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
