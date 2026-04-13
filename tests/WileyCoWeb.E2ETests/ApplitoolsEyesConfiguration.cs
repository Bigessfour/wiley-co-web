using System.Globalization;
using Applitools;
using Applitools.Utils.Geometry;

namespace WileyCoWeb.E2ETests;

internal static class ApplitoolsEyesConfiguration
{
    private const string DefaultAppName = "Wiley Widget";
    private const string DefaultBatchName = "Wiley Widget Visual Suite";
    private const int DefaultViewportWidth = 1280;
    private const int DefaultViewportHeight = 800;

    internal static EyesRunSettings Create(string testName, string apiKey)
    {
        var appName = GetValue("APPLITOOLS_APP_NAME") ?? DefaultAppName;
        var batchName = GetValue("APPLITOOLS_BATCH_NAME") ?? DefaultBatchName;
        var viewport = new RectangleSize(
            GetIntValue("APPLITOOLS_VIEWPORT_WIDTH", DefaultViewportWidth),
            GetIntValue("APPLITOOLS_VIEWPORT_HEIGHT", DefaultViewportHeight));

        var configuration = new Configuration();
        configuration.SetApiKey(apiKey);
        configuration.SetAppName(appName);
        configuration.SetTestName(testName);
        configuration.SetViewportSize(viewport);
        configuration.SetBatch(new BatchInfo(batchName));

        ApplyOptional(value => configuration.SetBranchName(value), "APPLITOOLS_BRANCH");
        ApplyOptional(value => configuration.SetParentBranchName(value), "APPLITOOLS_PARENT_BRANCH");
        ApplyOptional(value => configuration.SetBaselineBranchName(value), "APPLITOOLS_BASELINE_BRANCH");
        ApplyOptional(value => configuration.SetBaselineEnvName(value), "APPLITOOLS_BASELINE_ENV_NAME");
        ApplyOptional(value => configuration.SetServerUrl(value), "APPLITOOLS_SERVER_URL");

        var matchLevelValue = GetValue("APPLITOOLS_MATCH_LEVEL");
        if (Enum.TryParse<MatchLevel>(matchLevelValue, true, out var matchLevel))
        {
            configuration.SetMatchLevel(matchLevel);
        }

        return new EyesRunSettings(configuration, appName, batchName, viewport);
    }

    private static void ApplyOptional(Action<string> apply, string environmentVariableName)
    {
        var value = GetValue(environmentVariableName);
        if (!string.IsNullOrWhiteSpace(value))
        {
            apply(value);
        }
    }

    private static int GetIntValue(string environmentVariableName, int fallback)
    {
        var value = GetValue(environmentVariableName);
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;
    }

    private static string? GetValue(string environmentVariableName)
    {
        var processValue = Environment.GetEnvironmentVariable(environmentVariableName);
        if (!string.IsNullOrWhiteSpace(processValue))
        {
            return processValue;
        }

        var machineValue = Environment.GetEnvironmentVariable(environmentVariableName, EnvironmentVariableTarget.Machine);
        if (!string.IsNullOrWhiteSpace(machineValue))
        {
            return machineValue;
        }

        var userValue = Environment.GetEnvironmentVariable(environmentVariableName, EnvironmentVariableTarget.User);
        return string.IsNullOrWhiteSpace(userValue) ? null : userValue;
    }
}

internal sealed record EyesRunSettings(
    Configuration Configuration,
    string AppName,
    string BatchName,
    RectangleSize ViewportSize);