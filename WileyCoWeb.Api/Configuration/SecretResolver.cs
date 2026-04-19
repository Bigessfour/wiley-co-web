using Microsoft.Extensions.Configuration;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Amazon;

namespace WileyCoWeb.Api.Configuration;

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
    bool ConfigurationInjected);

public sealed partial class SecretResolver
{
    private readonly ConfigurationManager _configuration;

    public SecretResolver(ConfigurationManager configuration)
    {
        _configuration = configuration;
    }

    public async Task<XaiSecretResolutionResult> ResolveXaiSecretAsync()
    {
        var context = CreateResolutionContext();
        var configuredResult = TryResolveConfiguredKey(context);

        if (configuredResult is not null)
        {
            return configuredResult;
        }

        return await ResolveFromSecretsManagerAsync(context).ConfigureAwait(false);
    }
}