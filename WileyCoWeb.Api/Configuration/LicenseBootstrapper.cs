using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Syncfusion.Licensing;
using System.Text.Json;

namespace WileyCoWeb.Api.Configuration;

public static class LicenseBootstrapper
{
    public static async Task<SyncfusionLicenseResult> RegisterSyncfusionLicenseAsync(WebApplicationBuilder builder)
    {
        var resolution = await ResolveSyncfusionLicenseAsync(builder).ConfigureAwait(false);
        EmitResolutionLog(resolution);
        return resolution;
    }

    private static Task<SyncfusionLicenseResult> ResolveSyncfusionLicenseAsync(WebApplicationBuilder builder)
        => ResolveSyncfusionLicenseCoreAsync(builder);

    private static async Task<SyncfusionLicenseResult> ResolveSyncfusionLicenseCoreAsync(WebApplicationBuilder builder)
    {
        var syncfusionLicenseKey = TryResolveConfiguredSyncfusionLicenseKey(builder.Configuration, out var syncfusionKeySource);

        var localSettingsLicenseKey = await TryResolveLocalSettingsSyncfusionLicenseKeyAsync(builder.Environment, syncfusionLicenseKey).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(localSettingsLicenseKey))
        {
            syncfusionLicenseKey = localSettingsLicenseKey;
            syncfusionKeySource = "local-settings-file";
        }

        return new SyncfusionLicenseResult(syncfusionLicenseKey, syncfusionKeySource);
    }

    private static Task<string?> TryResolveLocalSettingsSyncfusionLicenseKeyAsync(IWebHostEnvironment environment, string? currentLicenseKey)
    {
        if (HasConfiguredSyncfusionLicenseKey(currentLicenseKey) || !CanLoadSyncfusionLicenseKeyFromLocalSettings(environment))
        {
            return Task.FromResult<string?>(null);
        }

        return LoadSyncfusionLicenseKeyFromLocalSettingsAsync(environment);
    }

    private static bool HasConfiguredSyncfusionLicenseKey(string? currentLicenseKey)
        => !string.IsNullOrWhiteSpace(currentLicenseKey);

    private static bool CanLoadSyncfusionLicenseKeyFromLocalSettings(IWebHostEnvironment environment)
        => environment.IsDevelopment() || environment.IsEnvironment("IntegrationTest");

    private static string? TryResolveConfiguredSyncfusionLicenseKey(IConfiguration configuration, out string syncfusionKeySource)
        => ResolveConfiguredSyncfusionLicenseKey(configuration, out syncfusionKeySource);

    private static string? ResolveConfiguredSyncfusionLicenseKey(IConfiguration configuration, out string syncfusionKeySource)
    {
        foreach (var candidate in GetConfiguredSyncfusionLicenseCandidates(configuration))
        {
            if (!string.IsNullOrWhiteSpace(candidate.LicenseKey))
            {
                syncfusionKeySource = candidate.KeySource;
                return candidate.LicenseKey;
            }
        }

        syncfusionKeySource = "not-found";
        return null;
    }

    private static IEnumerable<ConfiguredSyncfusionLicenseCandidate> GetConfiguredSyncfusionLicenseCandidates(IConfiguration configuration)
    {
        yield return new ConfiguredSyncfusionLicenseCandidate(configuration["SYNCFUSION_LICENSE_KEY"], "config:SYNCFUSION_LICENSE_KEY");
        yield return new ConfiguredSyncfusionLicenseCandidate(configuration["SyncfusionLicenseKey"], "config:SyncfusionLicenseKey");
        yield return new ConfiguredSyncfusionLicenseCandidate(Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY"), "env:SYNCFUSION_LICENSE_KEY");

        if (OperatingSystem.IsWindows())
        {
            yield return new ConfiguredSyncfusionLicenseCandidate(
                Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY", EnvironmentVariableTarget.User),
                "env-user:SYNCFUSION_LICENSE_KEY");
            yield return new ConfiguredSyncfusionLicenseCandidate(
                Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY", EnvironmentVariableTarget.Machine),
                "env-machine:SYNCFUSION_LICENSE_KEY");
        }
    }

    private static async Task<string?> LoadSyncfusionLicenseKeyFromLocalSettingsAsync(IWebHostEnvironment environment)
        => await LoadSyncfusionLicenseKeyFromLocalSettingsCoreAsync(environment).ConfigureAwait(false);

    private static async Task<string?> LoadSyncfusionLicenseKeyFromLocalSettingsCoreAsync(IWebHostEnvironment environment)
    {
        var localSettingsPath = GetLocalSettingsPath(environment);
        if (!File.Exists(localSettingsPath))
        {
            return null;
        }

        try
        {
            var settings = await LoadLocalSettingsAsync(localSettingsPath).ConfigureAwait(false);
            return TryExtractLocalSettingsLicenseKey(settings);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[API Startup] Failed to load Syncfusion license from local settings: {ex.Message}");
            return null;
        }
    }

    private static string GetLocalSettingsPath(IWebHostEnvironment environment)
        => Path.Combine(environment.ContentRootPath, "..", "appsettings.Syncfusion.local.json");

    private static async Task<Dictionary<string, object>?> LoadLocalSettingsAsync(string localSettingsPath)
    {
        var json = await File.ReadAllTextAsync(localSettingsPath).ConfigureAwait(false);
        return System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(json);
    }

    private static string? TryExtractLocalSettingsLicenseKey(Dictionary<string, object>? settings)
    {
        if (TryGetLocalSettingsString(settings, "SYNCFUSION_LICENSE_KEY", out var canonicalKey))
        {
            return canonicalKey;
        }

        if (TryGetLocalSettingsString(settings, "SyncfusionLicenseKey", out var namedKey))
        {
            return namedKey;
        }

        return null;
    }

    private static bool TryGetLocalSettingsString(Dictionary<string, object>? settings, string key, out string? value)
    {
        value = null;
        if (settings?.TryGetValue(key, out var rawValue) != true)
        {
            return false;
        }

        value = rawValue switch
        {
            string stringValue => stringValue,
            JsonElement { ValueKind: JsonValueKind.String } jsonElement => jsonElement.GetString(),
            JsonElement jsonElement => jsonElement.ToString(),
            _ => rawValue?.ToString()
        };

        value = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        return !string.IsNullOrWhiteSpace(value);
    }

    private sealed record ConfiguredSyncfusionLicenseCandidate(string? LicenseKey, string KeySource);
    private static void EmitResolutionLog(SyncfusionLicenseResult resolution)
    {
        if (!string.IsNullOrWhiteSpace(resolution.LicenseKey))
        {
            var trimmed = resolution.LicenseKey.Trim();
            SyncfusionLicenseProvider.RegisterLicense(trimmed);
            Console.WriteLine($"[API Startup] Syncfusion license key registered (source: {resolution.KeySource}, length: {trimmed.Length}).");
            return;
        }

        Console.WriteLine("[API Startup] WARNING: SYNCFUSION_LICENSE_KEY not found from any source (config, env, local settings). " +
            "Server-side PDF/Excel features may trigger license popups. Set SYNCFUSION_LICENSE_KEY env var or AWS Secrets Manager.");
    }

}

public sealed record SyncfusionLicenseResult(string? LicenseKey, string KeySource);