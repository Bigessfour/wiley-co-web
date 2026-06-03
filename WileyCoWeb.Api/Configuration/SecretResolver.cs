using Microsoft.Extensions.Configuration;
using WileyWidget.Services.Abstractions;

namespace WileyCoWeb.Api.Configuration;

/// <summary>
/// Simplified for local machine hosting (AWS cost decoupling).
/// Resolves XAI key ONLY from env/config (XAI_API_KEY, XAI:ApiKey, etc.) or falls back.
/// No AWS Secrets Manager / SSM / remote calls (use EncryptedLocalSecretVaultService for machine-bound secrets if needed).
/// See docs for local setup and migration.
/// </summary>
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
    string? SsmParameterName,
    bool SsmFetchAttempted,
    string SsmFetchStatus,
    string? SsmFetchErrorCode,
    string? SsmFetchErrorMessage);

public sealed partial class SecretResolver
{
    private readonly ConfigurationManager _configuration;
    private readonly ISecretVaultService? _localVault;

    /// <summary>
    /// Ctor for config-only (tests, legacy) or with local vault for machine-hosted secret support (post AWS removal).
    /// </summary>
    public SecretResolver(ConfigurationManager configuration, ISecretVaultService? localVault = null)
    {
        _configuration = configuration;
        _localVault = localVault;
    }

    // Legacy single-param ctor for direct tests that did new SecretResolver(config) — forwards to full.
    public SecretResolver(ConfigurationManager configuration) : this(configuration, null) { }

    public async Task<XaiSecretResolutionResult> ResolveXaiSecretAsync()
    {
        var context = CreateLocalResolutionContext();
        var configuredResult = TryResolveConfiguredKey(context);

        if (configuredResult is not null)
        {
            return configuredResult;
        }

        // After env/config: attempt local encrypted vault (DPAPI) if wired (for pure Windows machine hosting, cost-decoupled from AWS).
        // This is the preferred "no env, no checked-in config" path for town clerk installs.
        if (_localVault is not null)
        {
            try
            {
                string? vaultKey = await _localVault.GetSecretAsync("XAI_API_KEY").ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(vaultKey))
                    vaultKey = await _localVault.GetSecretAsync("XAI:ApiKey").ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(vaultKey))
                    vaultKey = await _localVault.GetSecretAsync("XaiApiKey").ConfigureAwait(false);

                if (!string.IsNullOrWhiteSpace(vaultKey))
                {
                    return new XaiSecretResolutionResult(
                        ResolvedKeySource: "vault:EncryptedLocalSecretVaultService",
                        EnvironmentKeyPresent: !string.IsNullOrWhiteSpace(context.EnvironmentApiKey),
                        ConfigDirectKeyPresent: !string.IsNullOrWhiteSpace(context.ConfigDirectApiKey),
                        ConfigNamedKeyPresent: !string.IsNullOrWhiteSpace(context.ConfigNamedApiKey),
                        SecretFetchAttempted: false,
                        SecretName: context.SecretName,
                        RegionName: context.RegionName,
                        SecretFetchStatus: "vault-hit",
                        SecretFetchErrorCode: null,
                        SecretFetchErrorMessage: null,
                        ConfigurationInjected: false,
                        SsmParameterName: context.SsmParameterName,
                        SsmFetchAttempted: false,
                        SsmFetchStatus: "skipped",
                        SsmFetchErrorCode: null,
                        SsmFetchErrorMessage: null);
                }
            }
            catch
            {
                // Resilient: vault init/permission issue should not crash startup; fall through to not-found guidance.
            }
        }

        // Local-only: no remote AWS. If no key, return not-found (local vault or env expected for machine hosting).
        return BuildLocalNotFoundResult(context);
    }

    private SecretResolutionContext CreateLocalResolutionContext()
    {
        return new SecretResolutionContext(
            SecretName: _configuration["XAI:SecretName"] ?? "Grok",
            RegionName: "local",
            EnvironmentApiKey: Environment.GetEnvironmentVariable("XAI_API_KEY"),
            ConfigDirectApiKey: _configuration["XAI_API_KEY"],
            ConfigNamedApiKey: _configuration["XAI:ApiKey"],
            SsmParameterName: null);
    }

    private static XaiSecretResolutionResult? TryResolveConfiguredKey(SecretResolutionContext context)
    {
        var result = ResolveConfiguredKey(context);
        return result;
    }

    private static XaiSecretResolutionResult BuildLocalNotFoundResult(SecretResolutionContext context)
    {
        return new XaiSecretResolutionResult(
            ResolvedKeySource: "not-found-local",
            EnvironmentKeyPresent: !string.IsNullOrWhiteSpace(context.EnvironmentApiKey),
            ConfigDirectKeyPresent: !string.IsNullOrWhiteSpace(context.ConfigDirectApiKey),
            ConfigNamedKeyPresent: !string.IsNullOrWhiteSpace(context.ConfigNamedApiKey),
            SecretFetchAttempted: false,
            SecretName: context.SecretName,
            RegionName: context.RegionName,
            SecretFetchStatus: "skipped-aws-for-local",
            SecretFetchErrorCode: null,
            SecretFetchErrorMessage: "AWS removed for local machine hosting. Set XAI_API_KEY env or config, or use EncryptedLocalSecretVaultService.",
            ConfigurationInjected: false,
            SsmParameterName: context.SsmParameterName,
            SsmFetchAttempted: false,
            SsmFetchStatus: "skipped",
            SsmFetchErrorCode: null,
            SsmFetchErrorMessage: null);
    }

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

    private static XaiSecretResolutionResult? ResolveConfiguredKey(SecretResolutionContext context)
    {
        // Simple local configured resolver (env/config only).
        if (!string.IsNullOrWhiteSpace(context.EnvironmentApiKey))
        {
            return new XaiSecretResolutionResult(
                ResolvedKeySource: "env:XAI_API_KEY",
                EnvironmentKeyPresent: true,
                ConfigDirectKeyPresent: false,
                ConfigNamedKeyPresent: false,
                SecretFetchAttempted: false,
                SecretName: context.SecretName,
                RegionName: context.RegionName,
                SecretFetchStatus: "configured",
                SecretFetchErrorCode: null,
                SecretFetchErrorMessage: null,
                ConfigurationInjected: false,
                SsmParameterName: null,
                SsmFetchAttempted: false,
                SsmFetchStatus: "skipped",
                SsmFetchErrorCode: null,
                SsmFetchErrorMessage: null);
        }
        if (!string.IsNullOrWhiteSpace(context.ConfigDirectApiKey))
        {
            return new XaiSecretResolutionResult(
                ResolvedKeySource: "config:XAI_API_KEY",
                EnvironmentKeyPresent: false,
                ConfigDirectKeyPresent: true,
                ConfigNamedKeyPresent: false,
                SecretFetchAttempted: false,
                SecretName: context.SecretName,
                RegionName: context.RegionName,
                SecretFetchStatus: "configured",
                SecretFetchErrorCode: null,
                SecretFetchErrorMessage: null,
                ConfigurationInjected: false,
                SsmParameterName: null,
                SsmFetchAttempted: false,
                SsmFetchStatus: "skipped",
                SsmFetchErrorCode: null,
                SsmFetchErrorMessage: null);
        }
        if (!string.IsNullOrWhiteSpace(context.ConfigNamedApiKey))
        {
            return new XaiSecretResolutionResult(
                ResolvedKeySource: "config:XAI:ApiKey",
                EnvironmentKeyPresent: false,
                ConfigDirectKeyPresent: false,
                ConfigNamedKeyPresent: true,
                SecretFetchAttempted: false,
                SecretName: context.SecretName,
                RegionName: context.RegionName,
                SecretFetchStatus: "configured",
                SecretFetchErrorCode: null,
                SecretFetchErrorMessage: null,
                ConfigurationInjected: false,
                SsmParameterName: null,
                SsmFetchAttempted: false,
                SsmFetchStatus: "skipped",
                SsmFetchErrorCode: null,
                SsmFetchErrorMessage: null);
        }
        return null;
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
}