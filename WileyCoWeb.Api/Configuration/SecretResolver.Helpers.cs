using Microsoft.Extensions.Configuration;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Amazon;

namespace WileyCoWeb.Api.Configuration;

public sealed partial class SecretResolver
{
    private SecretResolutionContext CreateResolutionContext()
    {
        return new SecretResolutionContext(
            SecretName: _configuration["XAI:SecretName"] ?? "Grok",
            RegionName: _configuration["AWS:Region"] ?? "us-east-1",
            EnvironmentApiKey: Environment.GetEnvironmentVariable("XAI_API_KEY"),
            ConfigDirectApiKey: _configuration["XAI_API_KEY"],
            ConfigNamedApiKey: _configuration["XAI:ApiKey"]);
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
        return TryExtractApiKey(secretValue);
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

    private static string? TryExtractApiKey(string? secretValue)
        => string.IsNullOrWhiteSpace(secretValue) ? null : TryExtractApiKeyCore(secretValue.Trim());

    private static string? TryExtractApiKeyCore(string trimmedSecretValue)
        => trimmedSecretValue.StartsWith('{')
            ? TryExtractApiKeyFromJson(trimmedSecretValue) ?? trimmedSecretValue
            : trimmedSecretValue;

    private static string? TryExtractApiKeyFromJson(string json)
    {
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(json);
            return TryExtractApiKeyFromJsonRoot(document.RootElement) ?? json;
        }
        catch (System.Text.Json.JsonException)
        {
            return json;
        }
    }

    private static string? TryExtractApiKeyFromJsonRoot(System.Text.Json.JsonElement root)
    {
        if (root.ValueKind != System.Text.Json.JsonValueKind.Object)
        {
            return null;
        }

        return TryReadApiKeyProperty(root);
    }

    private static string? TryReadApiKeyProperty(System.Text.Json.JsonElement root)
    {
        foreach (var propertyName in new[] { "XAI_API_KEY", "ApiKey", "XaiApiKey", "GrokApiKey", "XAI:ApiKey" })
        {
            if (root.TryGetProperty(propertyName, out var value) && value.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                return value.GetString();
            }
        }

        return null;
    }

    private void InjectResolvedApiKey(SecretResolutionContext context, string apiKey)
    {
        _configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["XAI_API_KEY"] = apiKey,
            ["XAI:ApiKey"] = apiKey,
            ["XAI:SecretName"] = context.SecretName
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
            ConfigurationInjected: false);
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
            ConfigurationInjected: false);
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
            ConfigurationInjected: true);
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
        string? ConfigNamedApiKey);

    private sealed record ConfiguredKeyResolution(
        string ResolvedKeySource,
        string SecretFetchStatus,
        bool EnvironmentKeyPresent,
        bool DirectConfigKeyPresent,
        bool NamedConfigKeyPresent);
}
