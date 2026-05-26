using Microsoft.Extensions.Configuration;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Amazon;
using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;

namespace WileyCoWeb.Api.Configuration;

public sealed partial class SecretResolver
{
    private SecretResolutionContext CreateResolutionContext()
    {
        // Support XAI:ParameterName (preferred), XAI:SSMParameterName, and XAI_SSM_PARAMETER_NAME env var.
        var ssmParam = _configuration["XAI:ParameterName"]
            ?? _configuration["XAI:SSMParameterName"]
            ?? Environment.GetEnvironmentVariable("XAI_SSM_PARAMETER_NAME")
            ?? Environment.GetEnvironmentVariable("XAI_SSM_PARAMETER");

        // Region: config AWS:Region or WILEY_AWS_REGION (appsettings), plus common envs, default us-east-2 (matches App Runner).
        var region = _configuration["AWS:Region"]
            ?? _configuration["WILEY_AWS_REGION"]
            ?? Environment.GetEnvironmentVariable("AWS_REGION")
            ?? Environment.GetEnvironmentVariable("WILEY_AWS_REGION")
            ?? "us-east-2";

        return new SecretResolutionContext(
            SecretName: _configuration["XAI:SecretName"] ?? "Grok",
            RegionName: region,
            EnvironmentApiKey: Environment.GetEnvironmentVariable("XAI_API_KEY"),
            ConfigDirectApiKey: _configuration["XAI_API_KEY"],
            ConfigNamedApiKey: _configuration["XAI:ApiKey"],
            SsmParameterName: string.IsNullOrWhiteSpace(ssmParam) ? null : ssmParam.Trim());
    }

    private static XaiSecretResolutionResult? TryResolveConfiguredKey(SecretResolutionContext context)
    {
        var configuredKey = ResolveConfiguredKey(context);
        if (configuredKey is null)
        {
            return null;
        }

        return BuildConfiguredResult(
            context,
            configuredKey.ResolvedKeySource,
            configuredKey.SecretFetchStatus,
            configuredKey.EnvironmentKeyPresent,
            configuredKey.DirectConfigKeyPresent,
            configuredKey.NamedConfigKeyPresent);
    }

    private async Task<XaiSecretResolutionResult> ResolveFromSecretsManagerAsync(SecretResolutionContext context)
    {
        try
        {
            var apiKey = await TryLoadSecretApiKeyAsync(context).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return BuildFailureResult(context, "secret_empty_or_invalid", null, "The secret was retrieved but did not contain a valid API key.");
            }

            InjectResolvedApiKey(context, apiKey);

            return BuildSuccessResult(context);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[API Startup] Failed to resolve xAI secret from AWS Secrets Manager: {ex.Message}");
            return BuildFailureResult(context, "failed", ex.GetType().Name, ex.Message);
        }
    }

    private async Task<string?> TryLoadSecretApiKeyAsync(SecretResolutionContext context)
    {
        var secretValue = await LoadSecretValueAsync(context).ConfigureAwait(false);
        return XaiApiKeyFormatter.ExtractUsableKey(secretValue);
    }

    private async Task<string?> LoadSecretValueAsync(SecretResolutionContext context)
    {
        using var client = new AmazonSecretsManagerClient(RegionEndpoint.GetBySystemName(context.RegionName));
        var response = await client.GetSecretValueAsync(new GetSecretValueRequest
        {
            SecretId = context.SecretName
        }).ConfigureAwait(false);

        return response.SecretString;
    }

    private void InjectResolvedApiKey(SecretResolutionContext context, string apiKey)
    {
        _configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["XAI_API_KEY"] = apiKey,
            ["XAI:ApiKey"] = apiKey,
            ["XAI:SecretName"] = context.SecretName,
            // Gap fix: ensure XAI:Enabled when key resolved via SSM/Secrets (prod path without persisted AppSettings EnableAI=true).
            ["XAI:Enabled"] = "true",
            ["EnableAI"] = "true"
        });
    }

    private static XaiSecretResolutionResult BuildConfiguredResult(
        SecretResolutionContext context,
        string resolvedKeySource,
        string secretFetchStatus,
        bool environmentKeyPresent,
        bool directConfigKeyPresent,
        bool namedConfigKeyPresent)
    {
        return new XaiSecretResolutionResult(
            ResolvedKeySource: resolvedKeySource,
            EnvironmentKeyPresent: environmentKeyPresent,
            ConfigDirectKeyPresent: directConfigKeyPresent,
            ConfigNamedKeyPresent: namedConfigKeyPresent,
            SecretFetchAttempted: false,
            SecretName: context.SecretName,
            RegionName: context.RegionName,
            SecretFetchStatus: secretFetchStatus,
            SecretFetchErrorCode: null,
            SecretFetchErrorMessage: null,
            ConfigurationInjected: false,
            SsmParameterName: context.SsmParameterName,
            SsmFetchAttempted: false,
            SsmFetchStatus: "not-attempted",
            SsmFetchErrorCode: null,
            SsmFetchErrorMessage: null);
    }

    private static XaiSecretResolutionResult BuildFailureResult(
        SecretResolutionContext context,
        string secretFetchStatus,
        string? secretFetchErrorCode,
        string? secretFetchErrorMessage)
    {
        return new XaiSecretResolutionResult(
            ResolvedKeySource: "not-found",
            EnvironmentKeyPresent: false,
            ConfigDirectKeyPresent: false,
            ConfigNamedKeyPresent: false,
            SecretFetchAttempted: true,
            SecretName: context.SecretName,
            RegionName: context.RegionName,
            SecretFetchStatus: secretFetchStatus,
            SecretFetchErrorCode: secretFetchErrorCode,
            SecretFetchErrorMessage: secretFetchErrorMessage,
            ConfigurationInjected: false,
            SsmParameterName: context.SsmParameterName,
            SsmFetchAttempted: false,
            SsmFetchStatus: "not-attempted",
            SsmFetchErrorCode: null,
            SsmFetchErrorMessage: null);
    }

    private static XaiSecretResolutionResult BuildSuccessResult(SecretResolutionContext context)
    {
        return new XaiSecretResolutionResult(
            ResolvedKeySource: $"secrets-manager:{context.SecretName}",
            EnvironmentKeyPresent: false,
            ConfigDirectKeyPresent: false,
            ConfigNamedKeyPresent: false,
            SecretFetchAttempted: true,
            SecretName: context.SecretName,
            RegionName: context.RegionName,
            SecretFetchStatus: "success",
            SecretFetchErrorCode: null,
            SecretFetchErrorMessage: null,
            ConfigurationInjected: true,
            SsmParameterName: context.SsmParameterName,
            SsmFetchAttempted: false,
            SsmFetchStatus: "not-attempted",
            SsmFetchErrorCode: null,
            SsmFetchErrorMessage: null);
    }

    // --- SSM Parameter Store support (inserted after env/config, before Secrets Manager; mirrors SM pattern) ---
    private async Task<XaiSecretResolutionResult?> TryResolveFromSsmAsync(SecretResolutionContext context)
    {
        if (string.IsNullOrWhiteSpace(context.SsmParameterName))
        {
            return null; // no SSM configured -> fall through to Secrets Manager
        }

        if (IsIntegrationTestEnvironment())
        {
            return BuildSsmSkippedResult(context);
        }

        try
        {
            var apiKey = await TryLoadSsmParameterApiKeyAsync(context).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return BuildSsmFailureResult(context, "ssm_empty_or_invalid", null, "The SSM parameter was retrieved but did not contain a valid API key.");
            }

            InjectResolvedApiKey(context, apiKey);

            return BuildSsmSuccessResult(context);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[API Startup] Failed to resolve xAI API key from SSM Parameter Store: {ex.Message}");
            return BuildSsmFailureResult(context, "failed", ex.GetType().Name, ex.Message);
        }
    }

    private async Task<string?> TryLoadSsmParameterApiKeyAsync(SecretResolutionContext context)
    {
        var parameterValue = await LoadSsmParameterValueAsync(context).ConfigureAwait(false);
        return XaiApiKeyFormatter.ExtractUsableKey(parameterValue);
    }

    private async Task<string?> LoadSsmParameterValueAsync(SecretResolutionContext context)
    {
        using var client = new AmazonSimpleSystemsManagementClient(RegionEndpoint.GetBySystemName(context.RegionName));
        var response = await client.GetParameterAsync(new GetParameterRequest
        {
            Name = context.SsmParameterName,
            WithDecryption = true
        }).ConfigureAwait(false);

        return response.Parameter?.Value;
    }

    private static XaiSecretResolutionResult BuildSsmSuccessResult(SecretResolutionContext context)
    {
        return new XaiSecretResolutionResult(
            ResolvedKeySource: $"ssm:{context.SsmParameterName}",
            EnvironmentKeyPresent: false,
            ConfigDirectKeyPresent: false,
            ConfigNamedKeyPresent: false,
            SecretFetchAttempted: false,
            SecretName: context.SecretName,
            RegionName: context.RegionName,
            SecretFetchStatus: "skipped_ssm_preferred",
            SecretFetchErrorCode: null,
            SecretFetchErrorMessage: null,
            ConfigurationInjected: true,
            SsmParameterName: context.SsmParameterName,
            SsmFetchAttempted: true,
            SsmFetchStatus: "success",
            SsmFetchErrorCode: null,
            SsmFetchErrorMessage: null);
    }

    private static XaiSecretResolutionResult BuildSsmFailureResult(
        SecretResolutionContext context,
        string ssmFetchStatus,
        string? ssmFetchErrorCode,
        string? ssmFetchErrorMessage)
    {
        return new XaiSecretResolutionResult(
            ResolvedKeySource: "not-found",
            EnvironmentKeyPresent: false,
            ConfigDirectKeyPresent: false,
            ConfigNamedKeyPresent: false,
            SecretFetchAttempted: false,
            SecretName: context.SecretName,
            RegionName: context.RegionName,
            SecretFetchStatus: ssmFetchStatus,
            SecretFetchErrorCode: ssmFetchErrorCode,
            SecretFetchErrorMessage: ssmFetchErrorMessage,
            ConfigurationInjected: false,
            SsmParameterName: context.SsmParameterName,
            SsmFetchAttempted: true,
            SsmFetchStatus: ssmFetchStatus,
            SsmFetchErrorCode: ssmFetchErrorCode,
            SsmFetchErrorMessage: ssmFetchErrorMessage);
    }

    private static XaiSecretResolutionResult BuildSsmSkippedResult(SecretResolutionContext context)
    {
        return new XaiSecretResolutionResult(
            ResolvedKeySource: "not-found",
            EnvironmentKeyPresent: false,
            ConfigDirectKeyPresent: false,
            ConfigNamedKeyPresent: false,
            SecretFetchAttempted: false,
            SecretName: context.SecretName,
            RegionName: context.RegionName,
            SecretFetchStatus: "skipped_integration_test",
            SecretFetchErrorCode: null,
            SecretFetchErrorMessage: null,
            ConfigurationInjected: false,
            SsmParameterName: context.SsmParameterName,
            SsmFetchAttempted: false,
            SsmFetchStatus: "skipped",
            SsmFetchErrorCode: null,
            SsmFetchErrorMessage: null);
    }

    private static ConfiguredKeyResolution? ResolveConfiguredKey(SecretResolutionContext context)
        => ResolveConfiguredKeyCore(context);

    private static ConfiguredKeyResolution? ResolveConfiguredKeyCore(SecretResolutionContext context)
    {
        foreach (var resolver in GetConfiguredKeyResolvers())
        {
            var configuredKey = resolver(context);
            if (configuredKey is not null)
            {
                return configuredKey;
            }
        }

        return null;
    }

    private static IEnumerable<Func<SecretResolutionContext, ConfiguredKeyResolution?>> GetConfiguredKeyResolvers()
    {
        yield return BuildEnvironmentConfiguredKey;
        yield return BuildDirectConfiguredKey;
        yield return BuildNamedConfiguredKey;
    }

    private static ConfiguredKeyResolution? BuildEnvironmentConfiguredKey(SecretResolutionContext context)
        => string.IsNullOrWhiteSpace(context.EnvironmentApiKey)
            ? null
            : new ConfiguredKeyResolution("env:XAI_API_KEY", "skipped_existing_environment_key", true, !string.IsNullOrWhiteSpace(context.ConfigDirectApiKey), !string.IsNullOrWhiteSpace(context.ConfigNamedApiKey));

    private static ConfiguredKeyResolution? BuildDirectConfiguredKey(SecretResolutionContext context)
        => string.IsNullOrWhiteSpace(context.ConfigDirectApiKey)
            ? null
            : new ConfiguredKeyResolution("config:XAI_API_KEY", "skipped_existing_direct_config_key", false, true, !string.IsNullOrWhiteSpace(context.ConfigNamedApiKey));

    private static ConfiguredKeyResolution? BuildNamedConfiguredKey(SecretResolutionContext context)
        => string.IsNullOrWhiteSpace(context.ConfigNamedApiKey)
            ? null
            : new ConfiguredKeyResolution("config:XAI:ApiKey", "skipped_existing_named_config_key", false, false, true);

    private sealed record SecretResolutionContext(
        string SecretName,
        string RegionName,
        string? EnvironmentApiKey,
        string? ConfigDirectApiKey,
        string? ConfigNamedApiKey,
        string? SsmParameterName);

    private sealed record ConfiguredKeyResolution(
        string ResolvedKeySource,
        string SecretFetchStatus,
        bool EnvironmentKeyPresent,
        bool DirectConfigKeyPresent,
        bool NamedConfigKeyPresent);
}
