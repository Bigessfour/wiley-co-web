using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using WileyCoWeb.Services.Auth;

namespace WileyCoWeb.Services;

static partial class ClientStartup
{
    static void ConfigureStaffAuthentication(WebAssemblyHostBuilder builder)
    {
        var cognitoAuthOptions = new CognitoAuthOptions();
        builder.Configuration.GetSection(CognitoAuthOptions.SectionName).Bind(cognitoAuthOptions);
        builder.Services.AddSingleton(Microsoft.Extensions.Options.Options.Create(cognitoAuthOptions));
        builder.Services.Configure<CognitoAuthOptions>(builder.Configuration.GetSection(CognitoAuthOptions.SectionName));

        var authEnabled = cognitoAuthOptions.Enabled;
        builder.Services.AddAuthorizationCore(options =>
        {
            if (authEnabled)
            {
                options.FallbackPolicy = new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .Build();
            }
        });

        builder.Services.AddScoped<CognitoTokenStore>();
        builder.Services.AddHttpClient("CognitoIdp", client => client.Timeout = TimeSpan.FromSeconds(30));
        builder.Services.AddScoped<CognitoAuthService>(sp =>
        {
            var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient("CognitoIdp");
            return new CognitoAuthService(
                httpClient,
                sp.GetRequiredService<CognitoTokenStore>(),
                sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<CognitoAuthOptions>>(),
                sp.GetRequiredService<ILogger<CognitoAuthService>>());
        });
        builder.Services.AddScoped<StaffAuthCoordinator>();
        builder.Services.AddScoped<IStaffAccessTokenProvider, StaffAccessTokenProvider>();
        builder.Services.AddScoped<CognitoAuthorizationMessageHandler>();

        if (authEnabled && cognitoAuthOptions.UsesHostedUi)
        {
            ConfigureHostedUiOidc(builder, cognitoAuthOptions);
            return;
        }

        builder.Services.AddScoped<CognitoAuthenticationStateProvider>();
        builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<CognitoAuthenticationStateProvider>());
        builder.Services.AddCascadingAuthenticationState();
    }

    static void ConfigureHostedUiOidc(WebAssemblyHostBuilder builder, CognitoAuthOptions authOptions)
    {
        var baseUri = new Uri(builder.HostEnvironment.BaseAddress);
        var redirectUri = string.IsNullOrWhiteSpace(authOptions.Cognito.RedirectUri)
            ? new Uri(baseUri, "authentication/login-callback").ToString()
            : authOptions.Cognito.RedirectUri.Trim();
        var postLogoutRedirectUri = string.IsNullOrWhiteSpace(authOptions.Cognito.PostLogoutRedirectUri)
            ? new Uri(baseUri, "/").ToString()
            : authOptions.Cognito.PostLogoutRedirectUri.Trim();

        builder.Services.AddOidcAuthentication(options =>
        {
            options.ProviderOptions.Authority = ResolveCognitoAuthority(authOptions);
            options.ProviderOptions.ClientId = authOptions.Cognito.ClientId.Trim();
            options.ProviderOptions.ResponseType = "code";
            options.ProviderOptions.RedirectUri = redirectUri;
            options.ProviderOptions.PostLogoutRedirectUri = postLogoutRedirectUri;
            options.ProviderOptions.DefaultScopes.Add("openid");
            options.ProviderOptions.DefaultScopes.Add("email");
            options.ProviderOptions.DefaultScopes.Add("profile");
            options.UserOptions.NameClaim = "name";
            options.UserOptions.RoleClaim = "cognito:groups";
        });

        builder.Services.AddCascadingAuthenticationState();
    }

    static string ResolveCognitoAuthority(CognitoAuthOptions authOptions)
    {
        if (!string.IsNullOrWhiteSpace(authOptions.Cognito.Authority))
        {
            return authOptions.Cognito.Authority.Trim().TrimEnd('/');
        }

        var region = string.IsNullOrWhiteSpace(authOptions.Cognito.Region) ? "us-east-2" : authOptions.Cognito.Region.Trim();
        var poolId = authOptions.Cognito.UserPoolId.Trim();
        return $"https://cognito-idp.{region}.amazonaws.com/{poolId}";
    }
}
