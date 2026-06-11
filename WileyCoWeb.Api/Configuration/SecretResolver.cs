using Microsoft.Extensions.Configuration;

namespace WileyCoWeb.Api.Configuration;

// Lean local-only secret resolver (post-AWS cull). No Amazon dependencies.
// Supports: direct XAI_API_KEY env, config (appsettings + .local + user secrets), named config.
// No remote fetches. Degraded mode / local prompt path handles missing key for Jarvis.
public sealed record XaiSecretResolutionResult(
    string ResolvedKeySource,
    bool EnvironmentKeyPresent,
    bool ConfigDirectKeyPresent,
    bool ConfigNamedKeyPresent,
    bool SecretFetchAttempted,
    string SecretName,
    string RegionName,
    string SecretFetchStatus,
    string? SecretFetchErrorCode,
    string? SecretFetchErrorMessage,
    bool ConfigurationInjected,
    string? SsmParameterName = null,
    bool SsmFetchAttempted = false,
    string SsmFetchStatus = "n/a",
    string? SsmFetchErrorCode = null,
    string? SsmFetchErrorMessage = null);

public sealed partial class SecretResolver
{
    private readonly ConfigurationManager _configuration;

    public SecretResolver(ConfigurationManager configuration)
    {
        _configuration = configuration;
    }

    public Task<XaiSecretResolutionResult> ResolveXaiSecretAsync()
    {
        var context = CreateResolutionContext();
        var configuredResult = TryResolveConfiguredKey(context);

        if (configuredResult is not null)
        {
            return Task.FromResult(configuredResult);
        }

        // Pure local path (no AWS). Key can be provided via in-app Jarvis prompt (writes to .local.json / user secrets).
        // Implementation provided in SecretResolver.Helpers.cs (local-only).
        return Task.FromResult(BuildNoRemoteAttemptResult(context));
    }

    // The rest of the implementation (CreateResolutionContext, TryResolveConfiguredKey, Build* helpers, local key resolution)
    // lives in SecretResolver.Helpers.cs for a clean separation after AWS bloat removal.
}