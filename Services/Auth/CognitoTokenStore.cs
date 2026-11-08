using System.Text.Json;
using Microsoft.JSInterop;

namespace WileyCoWeb.Services.Auth;

public sealed class CognitoTokenStore
{
    private const string StorageKey = "wiley.auth.cognito.session.v1";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IJSRuntime _jsRuntime;
    private CognitoAuthSession? _memorySession;

    public CognitoTokenStore(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task<CognitoAuthSession?> GetSessionAsync(CancellationToken cancellationToken = default)
    {
        if (_memorySession is not null)
        {
            return _memorySession;
        }

        try
        {
            var raw = await _jsRuntime.InvokeAsync<string?>("wileyWorkspaceStorage.getItem", cancellationToken, StorageKey)
                .ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            _memorySession = JsonSerializer.Deserialize<CognitoAuthSession>(raw, JsonOptions);
            return _memorySession;
        }
        catch (JSException)
        {
            return _memorySession;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public async Task SaveSessionAsync(CognitoAuthSession session, CancellationToken cancellationToken = default)
    {
        _memorySession = session;
        var raw = JsonSerializer.Serialize(session, JsonOptions);
        try
        {
            await _jsRuntime.InvokeVoidAsync("wileyWorkspaceStorage.setItem", cancellationToken, StorageKey, raw)
                .ConfigureAwait(false);
        }
        catch (JSException)
        {
            // Keep in-memory session when storage is unavailable (tests / private mode).
        }
    }

    public async Task ClearSessionAsync(CancellationToken cancellationToken = default)
    {
        _memorySession = null;
        try
        {
            await _jsRuntime.InvokeVoidAsync("wileyWorkspaceStorage.removeItem", cancellationToken, StorageKey)
                .ConfigureAwait(false);
        }
        catch (JSException)
        {
            // Ignore storage failures on sign-out.
        }
    }
}
