using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace WileyCoWeb.Api.Configuration;

/// <summary>
/// Pure local-only secret resolver (all AWS/SSM/SecretsManager removed during lean cull).
/// Resolves XAI key only from:
/// - Environment variable XAI_API_KEY
/// - Config XAI_API_KEY or XAI:ApiKey (supports appsettings.Development.local.json, user secrets, etc.)
/// The in-app Jarvis prompt persists the key locally for Development.
/// Returns a result that the startup can use for logging; no remote fetches ever.
/// </summary>
public sealed class SecretResolver
{
    private readonly ConfigurationManager _configuration;

    public SecretResolver(ConfigurationManager configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public Task<XaiSecretResolutionResult> ResolveXaiSecretAsync()
    {
        var envKey = Environment.GetEnvironmentVariable("XAI_API_KEY");
        var configDirect = _configuration["XAI_API_KEY"];
        var configNamed = _configuration["XAI:ApiKey"];

        if (!string.IsNullOrWhiteSpace(envKey))
        {
            return Task.FromResult(new XaiSecretResolutionResult(
                ResolvedKeySource: "environment:XAI_API_KEY",
                EnvironmentKeyPresent: true,
                ConfigDirectKeyPresent: false,
                ConfigNamedKeyPresent: false,
                SecretFetchAttempted: false,
                SecretName: "Grok",
                RegionName: "local",
                SecretFetchStatus: "success",
                SecretFetchErrorCode: null,
                SecretFetchErrorMessage: null,
                ConfigurationInjected: true));
        }

        if (!string.IsNullOrWhiteSpace(configDirect))
        {
            return Task.FromResult(new XaiSecretResolutionResult(
                ResolvedKeySource: "config:XAI_API_KEY",
                EnvironmentKeyPresent: false,
                ConfigDirectKeyPresent: true,
                ConfigNamedKeyPresent: false,
                SecretFetchAttempted: false,
                SecretName: "Grok",
                RegionName: "local",
                SecretFetchStatus: "success",
                SecretFetchErrorCode: null,
                SecretFetchErrorMessage: null,
                ConfigurationInjected: true));
        }

        if (!string.IsNullOrWhiteSpace(configNamed))
        {
            return Task.FromResult(new XaiSecretResolutionResult(
                ResolvedKeySource: "config:XAI:ApiKey",
                EnvironmentKeyPresent: false,
                ConfigDirectKeyPresent: false,
                ConfigNamedKeyPresent: true,
                SecretFetchAttempted: false,
                SecretName: "Grok",
                RegionName: "local",
                SecretFetchStatus: "success",
                SecretFetchErrorCode: null,
                SecretFetchErrorMessage: null,
                ConfigurationInjected: true));
        }

        // No key found locally. The Jarvis UI prompt will write it to Development.local.json / user secrets on first use.
        return Task.FromResult(new XaiSecretResolutionResult(
            ResolvedKeySource: "not-found",
            EnvironmentKeyPresent: false,
            ConfigDirectKeyPresent: false,
            ConfigNamedKeyPresent: false,
            SecretFetchAttempted: false,
            SecretName: "Grok",
            RegionName: "local",
            SecretFetchStatus: "local-only",
            SecretFetchErrorCode: null,
            SecretFetchErrorMessage: null,
            ConfigurationInjected: false));
    }
}

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
