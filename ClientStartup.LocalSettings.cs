using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http;
using System.Text.Json;

namespace WileyCoWeb.Services;

static partial class ClientStartup
{
    static string? NormalizeSyncfusionLicenseKey(string? rawLicenseKey)
    {
        if (string.IsNullOrWhiteSpace(rawLicenseKey)) return null;

        var normalizedLicenseKey = TryUnwrapWrappedQuotes(rawLicenseKey.Trim());

        return string.IsNullOrWhiteSpace(normalizedLicenseKey) ? null : normalizedLicenseKey;
    }

    static string TryUnwrapWrappedQuotes(string value)
    {
        if (!HasWrappedQuotes(value))
        {
            return value;
        }

        return value[1..^1].Trim();
    }

    static bool HasWrappedQuotes(string value)
        => value.Length > 1 && value.StartsWith('"') && value.EndsWith('"');

    static async Task<string?> LoadSyncfusionLicenseKeyFromLocalSettingsAsync(string baseAddress, IList<(LogLevel Level, string Message, Exception? Exception)> startupDiagnostics)
        => await LoadValidatedLocalSettingsPropertyAsync(baseAddress, "appsettings.Syncfusion.local.json", "The client will continue with environment/config fallback.", "SyncfusionLicenseKey", NormalizeSyncfusionLicenseKey, static value => !string.IsNullOrWhiteSpace(value), "appsettings.Syncfusion.local.json was loaded but did not contain SyncfusionLicenseKey. The client will continue with environment/config fallback.", static _ => "appsettings.Syncfusion.local.json contained SyncfusionLicenseKey, but the value was empty after normalization. The client will continue with environment/config fallback.", startupDiagnostics).ConfigureAwait(false);

    static async Task<string?> LoadWorkspaceApiBaseAddressFromLocalSettingsAsync(string baseAddress, IList<(LogLevel Level, string Message, Exception? Exception)> startupDiagnostics)
        => await LoadValidatedLocalSettingsPropertyAsync(baseAddress, "appsettings.Workspace.local.json", "The client will continue with environment/config/default fallback.", "WorkspaceApiBaseAddress", value => value.Trim(), static value => !string.IsNullOrWhiteSpace(value) && Uri.TryCreate(value, UriKind.Absolute, out _), "appsettings.Workspace.local.json was loaded but did not contain WorkspaceApiBaseAddress. The client will continue with environment/config/default fallback.", value => $"appsettings.Workspace.local.json contained a non-absolute WorkspaceApiBaseAddress '{value}'. The client will continue with environment/config/default fallback.", startupDiagnostics).ConfigureAwait(false);

    static async Task<string?> LoadValidatedLocalSettingsPropertyAsync(
        string baseAddress,
        string fileName,
        string fallbackMessage,
        string propertyName,
        Func<string, string?> normalizeValue,
        Func<string?, bool> isValidValue,
        string missingPropertyMessage,
        Func<string, string> invalidValueMessageFactory,
        IList<(LogLevel Level, string Message, Exception? Exception)> startupDiagnostics)
        => TryReadValidatedLocalSettingsProperty(
            await LoadLocalSettingsJsonAsync(baseAddress, fileName, fallbackMessage, startupDiagnostics).ConfigureAwait(false),
            fileName,
            propertyName,
            normalizeValue,
            isValidValue,
            missingPropertyMessage,
            invalidValueMessageFactory,
            fallbackMessage,
            startupDiagnostics);

    static async Task<string?> LoadLocalSettingsJsonAsync(
        string baseAddress,
        string fileName,
        string fallbackMessage,
        IList<(LogLevel Level, string Message, Exception? Exception)> startupDiagnostics)
        => await TryLoadLocalSettingsJsonAsync(baseAddress, fileName, fallbackMessage, startupDiagnostics).ConfigureAwait(false);

    static async Task<string?> TryLoadLocalSettingsJsonAsync(
        string baseAddress,
        string fileName,
        string fallbackMessage,
        IList<(LogLevel Level, string Message, Exception? Exception)> startupDiagnostics)
    {
        try
        {
            using var httpClient = CreateLocalSettingsHttpClient(baseAddress);
            return await httpClient.GetStringAsync(fileName).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return HandleLocalSettingsLoadException(startupDiagnostics, fileName, fallbackMessage, ex);
        }
    }

    static string? HandleLocalSettingsLoadException(
        IList<(LogLevel Level, string Message, Exception? Exception)> startupDiagnostics,
        string fileName,
        string fallbackMessage,
        Exception exception)
    {
        return exception switch
        {
            HttpRequestException httpRequestException => HandleHttpRequestLocalSettingsLoadFailure(startupDiagnostics, fileName, fallbackMessage, httpRequestException),
            _ => HandleLocalSettingsLoadFailure(startupDiagnostics, LogLevel.Warning, $"{fileName} could not be loaded. {fallbackMessage}", exception)
        };
    }

    static string? HandleHttpRequestLocalSettingsLoadFailure(
        IList<(LogLevel Level, string Message, Exception? Exception)> startupDiagnostics,
        string fileName,
        string fallbackMessage,
        HttpRequestException exception)
    {
        if (exception.StatusCode == HttpStatusCode.NotFound)
        {
            return HandleLocalSettingsLoadFailure(
                startupDiagnostics,
                LogLevel.Information,
                $"{fileName} was not found. {fallbackMessage}",
                null);
        }

        return HandleLocalSettingsLoadFailure(
            startupDiagnostics,
            LogLevel.Warning,
            $"{fileName} could not be loaded. {fallbackMessage}",
            exception);
    }

    static HttpClient CreateLocalSettingsHttpClient(string baseAddress)
        => new() { BaseAddress = new Uri(baseAddress) };

    static string? HandleLocalSettingsLoadFailure(
        IList<(LogLevel Level, string Message, Exception? Exception)> startupDiagnostics,
        LogLevel level,
        string message,
        Exception? exception)
    {
        AddLocalSettingsDiagnostic(startupDiagnostics, level, message, exception);
        return null;
    }

    static string? TryReadValidatedLocalSettingsProperty(
        string? localSettingsJson,
        string fileName,
        string propertyName,
        Func<string, string?> normalizeValue,
        Func<string?, bool> isValidValue,
        string missingPropertyMessage,
        Func<string, string> invalidValueMessageFactory,
        string fallbackMessage,
        IList<(LogLevel Level, string Message, Exception? Exception)> startupDiagnostics)
        => string.IsNullOrWhiteSpace(localSettingsJson)
            ? null
            : TryValidateLocalSettingsPropertyValue(TryReadLocalSettingsPropertyValue(localSettingsJson, fileName, propertyName, fallbackMessage, startupDiagnostics), normalizeValue, isValidValue, missingPropertyMessage, invalidValueMessageFactory, startupDiagnostics);

    static string? TryReadLocalSettingsPropertyValue(
        string localSettingsJson,
        string fileName,
        string propertyName,
        string fallbackMessage,
        IList<(LogLevel Level, string Message, Exception? Exception)> startupDiagnostics)
        => TryParseLocalSettingsPropertyValue(localSettingsJson, fileName, propertyName, fallbackMessage, startupDiagnostics);

    internal static string? TryParseLocalSettingsPropertyValue(
        string localSettingsJson,
        string fileName,
        string propertyName,
        string fallbackMessage,
        IList<(LogLevel Level, string Message, Exception? Exception)> startupDiagnostics)
        => string.IsNullOrWhiteSpace(localSettingsJson)
            ? null
            : TryReadLocalSettingsPropertyValueWithExceptionHandling(localSettingsJson, fileName, propertyName, fallbackMessage, startupDiagnostics);

    static string? TryReadLocalSettingsPropertyValueWithExceptionHandling(
        string localSettingsJson,
        string fileName,
        string propertyName,
        string fallbackMessage,
        IList<(LogLevel Level, string Message, Exception? Exception)> startupDiagnostics)
    {
        try { return ReadTrimmedLocalSettingsPropertyValue(localSettingsJson, propertyName); }
        catch (JsonException ex) { return HandleLocalSettingsParseFailure(startupDiagnostics, fileName, fallbackMessage, ex); }
    }

    static string? ReadTrimmedLocalSettingsPropertyValue(string localSettingsJson, string propertyName)
    {
        using var document = JsonDocument.Parse(localSettingsJson); return TryReadTrimmedJsonProperty(document, propertyName);
    }

    static string? HandleLocalSettingsParseFailure(
        IList<(LogLevel Level, string Message, Exception? Exception)> startupDiagnostics,
        string fileName,
        string fallbackMessage,
        JsonException exception)
        => HandleLocalSettingsLoadFailure(startupDiagnostics, LogLevel.Warning, $"{fileName} could not be parsed. {fallbackMessage}", exception);

    static string? TryValidateLocalSettingsPropertyValue(
        string? propertyValue,
        Func<string, string?> normalizeValue,
        Func<string?, bool> isValidValue,
        string missingPropertyMessage,
        Func<string, string> invalidValueMessageFactory,
        IList<(LogLevel Level, string Message, Exception? Exception)> startupDiagnostics)
        => string.IsNullOrWhiteSpace(propertyValue)
            ? HandleLocalSettingsLoadFailure(startupDiagnostics, LogLevel.Warning, missingPropertyMessage, null)
            : ValidateLocalSettingsPropertyValue(propertyValue, normalizeValue, isValidValue, invalidValueMessageFactory, startupDiagnostics);

    static string? ValidateLocalSettingsPropertyValue(
        string propertyValue,
        Func<string, string?> normalizeValue,
        Func<string?, bool> isValidValue,
        Func<string, string> invalidValueMessageFactory,
        IList<(LogLevel Level, string Message, Exception? Exception)> startupDiagnostics)
    {
        var normalizedValue = normalizeValue(propertyValue);
        if (!isValidValue(normalizedValue))
        {
            return HandleLocalSettingsLoadFailure(startupDiagnostics, LogLevel.Warning, invalidValueMessageFactory(propertyValue), null);
        }

        return normalizedValue;
    }

    static void AddLocalSettingsDiagnostic(
        IList<(LogLevel Level, string Message, Exception? Exception)> startupDiagnostics,
        LogLevel level,
        string message,
        Exception? exception)
    {
        startupDiagnostics.Add((level, message, exception));
    }

    static string? TryReadTrimmedJsonProperty(JsonDocument document, string propertyName)
    {
        if (!document.RootElement.TryGetProperty(propertyName, out var propertyValue))
        {
            return null;
        }

        return propertyValue.GetString()?.Trim();
    }
}
