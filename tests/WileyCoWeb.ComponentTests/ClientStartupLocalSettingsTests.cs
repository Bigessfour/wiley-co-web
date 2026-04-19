using Microsoft.Extensions.Logging;
using System.Reflection;
using System.Text.Json;

namespace WileyCoWeb.ComponentTests;

public sealed class ClientStartupLocalSettingsTests
{
    private static readonly MethodInfo TryParseLocalSettingsPropertyValueMethod =
        typeof(WileyCoWeb.Program)
            .Assembly
            .GetType("WileyCoWeb.Services.ClientStartup", throwOnError: true)!
            .GetMethod("TryParseLocalSettingsPropertyValue", BindingFlags.NonPublic | BindingFlags.Static)!;

    [Fact]
    public void TryParseLocalSettingsPropertyValue_ReturnsTrimmedPropertyValue_ForValidJson()
    {
        var diagnostics = new List<(LogLevel Level, string Message, Exception? Exception)>();

        var result = TryParseLocalSettingsPropertyValue(
            """{"SyncfusionLicenseKey":"  license-123  "}""",
            diagnostics);

        Assert.Equal("license-123", result);
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void TryParseLocalSettingsPropertyValue_LogsParseWarning_ForInvalidJson()
    {
        var diagnostics = new List<(LogLevel Level, string Message, Exception? Exception)>();

        var result = TryParseLocalSettingsPropertyValue("{", diagnostics);

        Assert.Null(result);
        Assert.Single(diagnostics);

        var (level, message, exception) = diagnostics[0];

        Assert.Equal(LogLevel.Warning, level);
        Assert.Contains("could not be parsed", message, StringComparison.OrdinalIgnoreCase);
        Assert.IsAssignableFrom<JsonException>(exception);
    }

    [Fact]
    public void TryParseLocalSettingsPropertyValue_LogsLoadWarning_ForNullJson()
    {
        var diagnostics = new List<(LogLevel Level, string Message, Exception? Exception)>();

        var result = TryParseLocalSettingsPropertyValue(null, diagnostics);

        Assert.Null(result);
        Assert.Single(diagnostics);

        var (level, message, exception) = diagnostics[0];

        Assert.Equal(LogLevel.Warning, level);
        Assert.Contains("could not be loaded", message, StringComparison.OrdinalIgnoreCase);
        Assert.IsType<ArgumentNullException>(exception);
    }

    private static string? TryParseLocalSettingsPropertyValue(
        string? localSettingsJson,
        IList<(LogLevel Level, string Message, Exception? Exception)> diagnostics)
        => (string?)TryParseLocalSettingsPropertyValueMethod.Invoke(
            null,
            new object?[]
            {
                localSettingsJson,
                "appsettings.Syncfusion.local.json",
                "SyncfusionLicenseKey",
                "The client will continue with environment/config fallback.",
                diagnostics
            });
}