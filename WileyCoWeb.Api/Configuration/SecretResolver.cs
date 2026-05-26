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
    bool ConfigurationInjected,
    // SSM Parameter Store fields (populated when XAI:ParameterName or XAI:SSMParameterName present; skipped in IntegrationTest)
    string? SsmParameterName,
    bool SsmFetchAttempted,
    string SsmFetchStatus,
    string? SsmFetchErrorCode,
    string? SsmFetchErrorMessage);

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

        // SSM before Secrets Manager (primary for xAI when ParameterName configured). Skip all remote fetches in IntegrationTest env (stub key from factory).
        if (!IsIntegrationTestEnvironment())
        {
            var ssmResult = await TryResolveFromSsmAsync(context).ConfigureAwait(false);
            if (ssmResult is not null)
            {
                return ssmResult;
            }
        }

        return await ResolveFromSecretsManagerAsync(context).ConfigureAwait(false);
    }

    private bool IsIntegrationTestEnvironment()
    {
        var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? _configuration["ASPNETCORE_ENVIRONMENT"]
            ?? _configuration["Environment"]
            ?? _configuration["ASPNETCORE__ENVIRONMENT"];
        return string.Equals(env, "IntegrationTest", StringComparison.OrdinalIgnoreCase);
    }
}