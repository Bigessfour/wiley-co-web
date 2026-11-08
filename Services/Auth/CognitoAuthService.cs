using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace WileyCoWeb.Services.Auth;

public sealed class CognitoAuthService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly CognitoTokenStore _tokenStore;
    private readonly CognitoAuthOptions _options;
    private readonly ILogger<CognitoAuthService> _logger;

    public CognitoAuthService(
        HttpClient httpClient,
        CognitoTokenStore tokenStore,
        IOptions<CognitoAuthOptions> options,
        ILogger<CognitoAuthService> logger)
    {
        _httpClient = httpClient;
        _tokenStore = tokenStore;
        _options = options.Value;
        _logger = logger;
    }

    public bool IsEnabled => _options.Enabled
        && !string.IsNullOrWhiteSpace(_options.Cognito.ClientId)
        && !string.IsNullOrWhiteSpace(_options.Cognito.Region);

    public async Task<CognitoAuthSession?> GetSessionAsync(CancellationToken cancellationToken = default)
        => await _tokenStore.GetSessionAsync(cancellationToken).ConfigureAwait(false);

    public async Task<CognitoAuthSession> SignInAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
        {
            throw new InvalidOperationException("Cognito staff auth is not enabled for this client build.");
        }

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException("Username and password are required.");
        }

        var payload = new CognitoInitiateAuthRequest
        {
            AuthFlow = "USER_PASSWORD_AUTH",
            ClientId = _options.Cognito.ClientId.Trim(),
            AuthParameters = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["USERNAME"] = username.Trim(),
                ["PASSWORD"] = password
            }
        };

        using var request = CreateCognitoRequest("AWSCognitoIdentityProviderService.InitiateAuth", payload);
        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(FormatCognitoError(body, "Sign-in failed."));
        }

        var result = JsonSerializer.Deserialize<CognitoAuthResponse>(body, JsonOptions)
            ?? throw new InvalidOperationException("Cognito returned an empty sign-in response.");

        if (!string.IsNullOrWhiteSpace(result.ChallengeName))
        {
            throw new InvalidOperationException(
                $"Cognito requires challenge '{result.ChallengeName}'. Complete the challenge in the AWS console, then sign in again.");
        }

        var session = ToSession(result, username.Trim());
        await _tokenStore.SaveSessionAsync(session, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Cognito staff sign-in succeeded for {Username}.", session.Username);
        return session;
    }

    public async Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        await _tokenStore.ClearSessionAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        var session = await _tokenStore.GetSessionAsync(cancellationToken).ConfigureAwait(false);
        if (session is null || string.IsNullOrWhiteSpace(session.AccessToken))
        {
            return null;
        }

        if (!session.IsAccessTokenExpired(TimeSpan.FromMinutes(2)))
        {
            return session.AccessToken;
        }

        if (string.IsNullOrWhiteSpace(session.RefreshToken))
        {
            await _tokenStore.ClearSessionAsync(cancellationToken).ConfigureAwait(false);
            return null;
        }

        try
        {
            var refreshed = await RefreshAsync(session, cancellationToken).ConfigureAwait(false);
            return refreshed.AccessToken;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cognito token refresh failed; clearing staff session.");
            await _tokenStore.ClearSessionAsync(cancellationToken).ConfigureAwait(false);
            return null;
        }
    }

    private async Task<CognitoAuthSession> RefreshAsync(CognitoAuthSession session, CancellationToken cancellationToken)
    {
        var payload = new CognitoInitiateAuthRequest
        {
            AuthFlow = "REFRESH_TOKEN_AUTH",
            ClientId = _options.Cognito.ClientId.Trim(),
            AuthParameters = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["REFRESH_TOKEN"] = session.RefreshToken
            }
        };

        using var request = CreateCognitoRequest("AWSCognitoIdentityProviderService.InitiateAuth", payload);
        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(FormatCognitoError(body, "Token refresh failed."));
        }

        var result = JsonSerializer.Deserialize<CognitoAuthResponse>(body, JsonOptions)
            ?? throw new InvalidOperationException("Cognito returned an empty refresh response.");

        var refreshed = ToSession(result, session.Username, session.RefreshToken);
        await _tokenStore.SaveSessionAsync(refreshed, cancellationToken).ConfigureAwait(false);
        return refreshed;
    }

    private HttpRequestMessage CreateCognitoRequest<TPayload>(string target, TPayload payload)
    {
        var region = string.IsNullOrWhiteSpace(_options.Cognito.Region) ? "us-east-2" : _options.Cognito.Region.Trim();
        var request = new HttpRequestMessage(HttpMethod.Post, $"https://cognito-idp.{region}.amazonaws.com/");
        request.Headers.TryAddWithoutValidation("X-Amz-Target", target);
        request.Content = JsonContent.Create(payload, options: JsonOptions);
        request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/x-amz-json-1.1");
        return request;
    }

    private static CognitoAuthSession ToSession(CognitoAuthResponse response, string username, string? existingRefreshToken = null)
    {
        var auth = response.AuthenticationResult
            ?? throw new InvalidOperationException("Cognito did not return AuthenticationResult.");

        if (string.IsNullOrWhiteSpace(auth.AccessToken))
        {
            throw new InvalidOperationException("Cognito did not return an access token.");
        }

        var expiresInSeconds = auth.ExpiresIn > 0 ? auth.ExpiresIn : 3600;
        return new CognitoAuthSession
        {
            AccessToken = auth.AccessToken,
            IdToken = auth.IdToken ?? string.Empty,
            RefreshToken = string.IsNullOrWhiteSpace(auth.RefreshToken) ? existingRefreshToken ?? string.Empty : auth.RefreshToken,
            AccessTokenExpiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(expiresInSeconds),
            Username = username
        };
    }

    private static string FormatCognitoError(string body, string fallback)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("message", out var message)
                && message.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(message.GetString()))
            {
                return message.GetString()!;
            }

            if (document.RootElement.TryGetProperty("__type", out var type)
                && type.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(type.GetString()))
            {
                return type.GetString()!;
            }
        }
        catch (JsonException)
        {
            // Fall through to default message.
        }

        return string.IsNullOrWhiteSpace(body) ? fallback : body;
    }

    private sealed class CognitoInitiateAuthRequest
    {
        public string AuthFlow { get; set; } = string.Empty;

        public string ClientId { get; set; } = string.Empty;

        public Dictionary<string, string> AuthParameters { get; set; } = new(StringComparer.Ordinal);
    }

    private sealed class CognitoAuthResponse
    {
        public string? ChallengeName { get; set; }

        public CognitoAuthenticationResult? AuthenticationResult { get; set; }
    }

    private sealed class CognitoAuthenticationResult
    {
        public string? AccessToken { get; set; }

        public string? IdToken { get; set; }

        public string? RefreshToken { get; set; }

        [JsonPropertyName("ExpiresIn")]
        public int ExpiresIn { get; set; }
    }
}
