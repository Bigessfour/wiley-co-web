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

        // In Development (local dotnet run without AWS creds), skip remote AWS fetches by default.
        // Production / App Runner will have the ParameterName/SecretName values and will attempt.
        // Use XAI:ForceRemoteSecretResolutionInDevelopment=true (or set a direct XAI_API_KEY) to force.
        if (ShouldAttemptRemoteAwsResolution())
        {
            // SSM before Secrets Manager (primary for xAI when ParameterName configured).
            // Skip all remote fetches in IntegrationTest env (stub key from factory).
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

        // No remote resolution attempted (typical local Development path).
        return BuildNoRemoteAttemptResult(context);
    }

    private bool ShouldAttemptRemoteAwsResolution()
    {
        if (IsIntegrationTestEnvironment())
        {
            return false;
        }

        // Check common ways Development is signaled.
        var aspnetEnv = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? _configuration["ASPNETCORE_ENVIRONMENT"]
            ?? _configuration["Environment"]
            ?? _configuration["ASPNETCORE__ENVIRONMENT"];

        if (string.Equals(aspnetEnv, "Development", StringComparison.OrdinalIgnoreCase))
        {
            var force = _configuration.GetValue<bool?>("XAI:ForceRemoteSecretResolutionInDevelopment") ?? false;
            return force;
        }

        // Non-Development environments (Production, Staging, etc.) attempt remote when names are configured.
        return true;
    }

    private static XaiSecretResolutionResult BuildNoRemoteAttemptResult(SecretResolutionContext context)
    {
        return new XaiSecretResolutionResult(
            ResolvedKeySource: "not-found",
            EnvironmentKeyPresent: false,
            ConfigDirectKeyPresent: false,
            ConfigNamedKeyPresent: false,
            SecretFetchAttempted: false,
            SecretName: context.SecretName,
            RegionName: context.RegionName,
            SecretFetchStatus: "skipped_development",
            SecretFetchErrorCode: null,
            SecretFetchErrorMessage: null,
            ConfigurationInjected: false,
            SsmParameterName: context.SsmParameterName,
            SsmFetchAttempted: false,
            SsmFetchStatus: "skipped_development",
            SsmFetchErrorCode: null,
            SsmFetchErrorMessage: null);
    }

    private bool IsIntegrationTestEnvironment()
    {
        var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? _configuration["ASPNETCORE_ENVIRONMENT"]
            ?? _configuration["Environment"]
            ?? _configuration["ASPNETCORE__ENVIRONMENT"];
        return string.Equals(env, "IntegrationTest", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDevelopmentEnvironment()
    {
        var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
        return string.Equals(env, "Development", StringComparison.OrdinalIgnoreCase);
    }
}