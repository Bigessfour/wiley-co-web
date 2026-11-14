using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.Extensions.Options;

namespace WileyCoWeb.Services.Auth;

public interface IStaffAccessTokenProvider
{
    Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default);
}

public sealed class StaffAccessTokenProvider : IStaffAccessTokenProvider
{
    private readonly CognitoAuthOptions _options;
    private readonly CognitoAuthService _passwordAuth;
    private readonly IAccessTokenProvider? _oidcAccessTokenProvider;

    public StaffAccessTokenProvider(
        IOptions<CognitoAuthOptions> options,
        CognitoAuthService passwordAuth,
        IServiceProvider services)
    {
        _options = options.Value;
        _passwordAuth = passwordAuth;
        _oidcAccessTokenProvider = _options.UsesHostedUi
            ? services.GetService<IAccessTokenProvider>()
            : null;
    }

    public async Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return null;
        }

        if (_options.UsesHostedUi)
        {
            if (_oidcAccessTokenProvider is null)
            {
                return null;
            }

            var result = await _oidcAccessTokenProvider.RequestAccessToken().ConfigureAwait(false);
            if (result.TryGetToken(out var token))
            {
                return token.Value;
            }

            return null;
        }

        return await _passwordAuth.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
    }
}
